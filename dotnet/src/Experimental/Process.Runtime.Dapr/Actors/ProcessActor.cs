// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Dapr.Actors.Runtime;
using Dapr.Actors;

namespace Microsoft.SemanticKernel.Process.Actors;
internal class ProcessActor : StepActor, IProcess, IDisposable
{
    private const string EndStepId = "Microsoft.SemanticKernel.Process.EndStep";
    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly Channel<KernelProcessEvent> _externalEventChannel;
    private readonly ILogger _logger;

    internal readonly List<IStep> _steps = [];
    internal readonly Kernel _kernel;

    internal List<KernelProcessStepInfo>? _stepsInfos;
    internal KernelProcess? _process;
    private JoinableTask? _processTask;
    private CancellationTokenSource? _processCancelSource;
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessActor"/> class.
    /// </summary>
    /// <param name="host">The Dapr host actor</param>
    /// <param name="kernel">An instance of <see cref="Kernel"/></param>
    /// <param name="loggerFactory">Optional. A <see cref="ILoggerFactory"/>.</param>
    internal ProcessActor(ActorHost host, Kernel kernel, ILoggerFactory? loggerFactory)
        : base(host, kernel, loggerFactory)
    {
        this._kernel = kernel;
        this._externalEventChannel = Channel.CreateUnbounded<KernelProcessEvent>();
        this._joinableTaskContext = new JoinableTaskContext();
        this._joinableTaskFactory = new JoinableTaskFactory(this._joinableTaskContext);
        this._logger = this.LoggerFactory?.CreateLogger(this.Name) ?? new NullLogger<StepActor>();
    }

    #region Public Actor Methods

    public async ValueTask InitializeProcessAsync(KernelProcess process, string? parentProcessId)
    {
        Verify.NotNull(process);
        Verify.NotNull(process.Steps);

        this._stepsInfos = new List<KernelProcessStepInfo>(process.Steps);
        this._process = process;

        // Initialize the input and output edges for the process
        this._outputEdges = this._process.Edges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());

        // Initialize the steps within this process
        foreach (var step in this._stepsInfos)
        {
            IStep? stepActor = null;

            // The current step should already have a name.
            Verify.NotNull(step.State?.Name);

            if (step is KernelProcess kernelStep)
            {
                // The process will only have an Id if its already been executed.
                if (string.IsNullOrWhiteSpace(kernelStep.State.Id))
                {
                    kernelStep = kernelStep with { State = kernelStep.State with { Id = Guid.NewGuid().ToString() } };
                }

                // Initialize the step as a process.
                var processId = new ActorId(kernelStep.State.Id!);
                var processActor = this.ProxyFactory.CreateActorProxy<IProcess>(processId, nameof(ProcessActor));
                await processActor.InitializeProcessAsync(kernelStep, this.Id.GetId()).ConfigureAwait(false);
                stepActor = this.ProxyFactory.CreateActorProxy<IStep>(processId, nameof(ProcessActor));
            }
            else
            {
                // The current step should already have an Id.
                Verify.NotNull(step.State?.Id);

                var stepId = new ActorId(step.State.Id!);
                stepActor = this.ProxyFactory.CreateActorProxy<IStep>(stepId, nameof(StepActor));
                await stepActor.InitializeStepAsync(step, this.Id.GetId()).ConfigureAwait(false);
            }

            this._steps.Add(stepActor);
        }

        this._isInitialized = true;
    }

    /// <summary>
    /// Starts the process with an initial event and an optional kernel.
    /// </summary>
    /// <param name="kernel">The <see cref="Kernel"/> instance to use within the running process.</param>
    /// <param name="keepAlive">Indicates if the process should wait for external events after it's finished processing.</param>
    /// <returns> <see cref="Task"/></returns>
    public Task StartAsync(Kernel? kernel = null, bool keepAlive = true)
    {
        if (!this._isInitialized)
        {
            throw new InvalidOperationException("The process cannot be started before it has been initialized.");
        }

        this._processCancelSource = new CancellationTokenSource();
        this._processTask = this._joinableTaskFactory.RunAsync(()
            => this.Internal_ExecuteAsync(kernel, keepAlive: keepAlive, cancellationToken: this._processCancelSource.Token));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the process with an initial event and then waits for the process to finish. In this case the process will not
    /// keep alive waiting for external events after the internal messages have stopped.
    /// </summary>
    /// <param name="processEvent">Required. The <see cref="KernelProcessEvent"/> to start the process with.</param>
    /// <param name="kernel">Optional. A <see cref="Kernel"/> to use when executing the process.</param>
    /// <returns>A <see cref="Task"/></returns>
    public async Task RunOnceAsync(KernelProcessEvent? processEvent, Kernel? kernel = null)
    {
        Verify.NotNull(processEvent);
        await this._externalEventChannel.Writer.WriteAsync(processEvent).ConfigureAwait(false);
        await this.StartAsync(kernel, keepAlive: false).ConfigureAwait(false);
        await this._processTask!.JoinAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops a running process. This will cancel the process and wait for it to complete before returning.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    public async Task StopAsync()
    {
        if (this._processTask is null || this._processCancelSource is null || this._processTask.IsCompleted)
        {
            return;
        }

        // Cancel the process and wait for it to complete.
        this._processCancelSource.Cancel();

        try
        {
            await this._processTask;
        }
        catch (OperationCanceledException)
        {
            // The task was cancelled, so we can ignore this exception.
        }
        finally
        {
            this._processCancelSource.Dispose();
        }
    }

    /// <summary>
    /// Sends a message to the process. This does not start the process if it's not already running, in
    /// this case the message will remain queued until the process is started.
    /// </summary>
    /// <param name="processEvent">Required. The <see cref="KernelProcessEvent"/> to start the process with.</param>
    /// <param name="kernel">Optional. A <see cref="Kernel"/> to use when executing the process.</param>
    /// <returns>A <see cref="Task"/></returns>
    public async Task SendMessageAsync(KernelProcessEvent processEvent, Kernel? kernel = null)
    {
        Verify.NotNull(processEvent);
        await this._externalEventChannel.Writer.WriteAsync(processEvent).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the process information.
    /// </summary>
    /// <returns>An instance of <see cref="KernelProcess"/></returns>
    public async Task<KernelProcess> GetProcessInfoAsync()
    {
        return await this.ToKernelProcessAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// When the process is used as a step within another process, this method will be called
    /// rather than ToKernelProcessAsync when extracting the state.
    /// </summary>
    /// <returns>A <see cref="Task{T}"/> where T is <see cref="KernelProcess"/></returns>
    public override async Task<KernelProcessStepInfo> ToKernelProcessStepInfoAsync()
    {
        return await this.ToKernelProcessAsync().ConfigureAwait(false);
    }

    #endregion

    /// <summary>
    /// Handles a <see cref="DaprMessage"/> that has been sent to the process. This happens only in the case
    /// of a process (this one) running as a step within another process (this one's parent). In this case the
    /// entire sub-process should be executed within a single superstep.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <returns>A <see cref="Task"/></returns>
    /// <exception cref="KernelException"></exception>
    protected override async Task HandleMessageAsync(DaprMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.TargetEventId))
        {
            string errorMessage = "Internal Process Error: The target event id must be specified when sending a message to a step.";
            this._logger.LogError("{ErrorMessage}", errorMessage);
            throw new KernelException(errorMessage);
        }

        string eventId = message.TargetEventId!;
        if (this._outputEdges!.TryGetValue(eventId, out List<KernelProcessEdge>? edges) && edges is not null)
        {
            foreach (var edge in edges)
            {
                // Create the external event that will be used to start the nested process. Since this event came
                // from outside this processes, we set the visibility to internal so that it's not emitted back out again.
                var nestedEvent = new KernelProcessEvent() { Id = eventId, Data = message.TargetEventData, Visibility = KernelProcessEventVisibility.Internal };

                // Run the nested process completely within a single superstep.
                await this.RunOnceAsync(nestedEvent, this._kernel).ConfigureAwait(false);
            }
        }
    }

    #region Private Methods

    /// <summary>
    /// Initializes this process as a step within another process.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/></returns>
    /// <exception cref="KernelException"></exception>
    protected override ValueTask ActivateStepAsync()
    {
        // The process does not need any further initialization as it's already been initialized.
        // Override the base method to prevent it from being called.
        return default;
    }

    private async Task Internal_ExecuteAsync(Kernel? kernel = null, int maxSupersteps = 100, bool keepAlive = true, CancellationToken cancellationToken = default)
    {
        Kernel localKernel = kernel ?? this._kernel;
        Queue<DaprMessage> messageChannel = new();

        try
        {
            // Run the Pregel algorithm until there are no more messages being sent.
            for (int superstep = 0; superstep < maxSupersteps; superstep++)
            {
                // Check for EndStep messages. If thare are any then cancel the process.
                if (await this.IsEndMessageSentAsync().ConfigureAwait(false))
                {
                    this._processCancelSource?.Cancel();
                    break;
                }

                // Check for external events
                await this.EnqueueExternalMessagesAsync().ConfigureAwait(false);

                // Reach out to all of the steps in the process and instruct them to retrieive their pending messages from their associated queues.
                var stepPreparationTasks = this._steps.Select(step => step.PrepareIncomingMessagesAsync()).ToList();
                var messageCounts = await Task.WhenAll(stepPreparationTasks).ConfigureAwait(false);

                // If there are no messages to process, wait for an external event or finish.
                if (messageCounts.Sum() == 0)
                {
                    if (!keepAlive || !await this._externalEventChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        this._processCancelSource?.Cancel();
                        break;
                    }
                }

                // Process the incoming messages for each step.
                var stepProcessingTasks = this._steps.Select(step => step.ProcessIncomingMessagesAsync()).ToList();
                await Task.WhenAll(stepProcessingTasks).ConfigureAwait(false);

                // Handle public events that need to be bubbled out of the process.
                var eventQueue = this.ProxyFactory.CreateActorProxy<IEventQueue>(new ActorId(this.Id.GetId()), nameof(EventQueueActor));
                var allEvents = await eventQueue.DequeueAllAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._logger?.LogError("An error occurred while running the process: {ErrorMessage}.", ex.Message);
            throw;
        }
        finally
        {
            if (this._processCancelSource?.IsCancellationRequested ?? false)
            {
                this._processCancelSource.Cancel();
            }

            this._processCancelSource?.Dispose();
        }

        return;
    }

    /// <summary>
    /// Processes external events that have been sent to the process, translates them to <see cref="DaprMessage"/>s, and enqueues
    /// them to the provided message channel so that they can be processesed in the next superstep.
    /// </summary>
    private async Task EnqueueExternalMessagesAsync()
    {
        var externalEventQueue = this.ProxyFactory.CreateActorProxy<IExternalEventQueue>(new ActorId(this.Id.GetId()), nameof(ExternalEventQueueActor));
        var externalEvents = await externalEventQueue.DequeueAllAsync().ConfigureAwait(false);

        foreach (var externalEvent in externalEvents)
        {
            if (this._outputEdges!.TryGetValue(externalEvent.Id!, out List<KernelProcessEdge>? edges) && edges is not null)
            {
                foreach (var edge in edges)
                {
                    DaprMessage message = DaprMessageFactory.CreateFromEdge(edge, externalEvent.Data);
                    var messageQueue = this.ProxyFactory.CreateActorProxy<IMessageQueue>(new ActorId(edge.OutputTarget.StepId), nameof(MessageQueueActor));
                    await messageQueue.EnqueueAsync(message).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Determines is the end message has been sent to the process.
    /// </summary>
    /// <returns>True if the end message has been sent, otherwise false.</returns>
    private async Task<bool> IsEndMessageSentAsync()
    {
        var endMessageQueue = this.ProxyFactory.CreateActorProxy<IMessageQueue>(new ActorId(EndStepId), nameof(MessageQueueActor));
        var messages = await endMessageQueue.DequeueAllAsync().ConfigureAwait(false);
        return messages.Count > 0;
    }

    /// <summary>
    /// Builds a <see cref="KernelProcess"/> from the current <see cref="ProcessActor"/>.
    /// </summary>
    /// <returns>An instance of <see cref="KernelProcess"/></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<KernelProcess> ToKernelProcessAsync()
    {
        var processState = new KernelProcessState(this.Name, this.Id.GetId());
        var stepTasks = this._steps.Select(step => step.ToKernelProcessStepInfoAsync()).ToList();
        var steps = await Task.WhenAll(stepTasks).ConfigureAwait(false);
        return new KernelProcess(processState, steps, this._outputEdges);
    }

    #endregion

    public void Dispose()
    {
        this._externalEventChannel.Writer.Complete();
        this._joinableTaskContext.Dispose();
        this._joinableTaskContext.Dispose();
        this._processCancelSource?.Dispose();
    }
}
