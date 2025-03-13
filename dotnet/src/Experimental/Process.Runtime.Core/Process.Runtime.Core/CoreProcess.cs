// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Channels;
using Microsoft.AutoGen.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Runtime;
using Microsoft.SemanticKernel.Process.Serialization;
using Microsoft.VisualStudio.Threading;
using Process.Runtime.Core;

namespace Microsoft.SemanticKernel;

internal sealed class CoreProcess : CoreStep, IDisposable, IHandle<RunOnce>, IHandle<ToProcessStepInfo, ProcessStepInfo>
{
    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly Channel<KernelProcessEvent> _externalEventChannel;
    private readonly AgentId _id;
    private readonly IAgentRuntime _runtime;

    internal readonly List<ProcessStepInfo> _steps = [];

    internal IList<ProcessStepInfo>? _stepsInfos;
    internal ProcessStepInfo? _process;
    private JoinableTask? _processTask;
    private CancellationTokenSource? _processCancelSource;
    private bool _isInitialized;
    private ILogger? _processLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoreProcess"/> class.
    /// </summary>
    /// <param name="id">The unique Id of the processes</param>
    /// <param name="runtime">The runtime.</param>
    /// <param name="kernel">An instance of <see cref="Kernel"/></param>
    /// <param name="logger">Optional. An instance of <see cref="ILogger"/></param>
    public CoreProcess(AgentId id, IAgentRuntime runtime, Kernel kernel, ILogger<CoreProcess>? logger = null)
        : base(id, runtime, kernel, logger)
    {
        this._id = id;
        this._runtime = runtime;

        this._externalEventChannel = Channel.CreateUnbounded<KernelProcessEvent>();
        this._joinableTaskContext = new JoinableTaskContext();
        this._joinableTaskFactory = new JoinableTaskFactory(this._joinableTaskContext);
        this._processLogger = logger ?? new NullLogger<CoreProcess>();
    }

    #region Public Actor Methods

    //public async Task InitializeProcessAsync(ProcessStepInfo processInfo, string? parentProcessId, string? eventProxyStepId = null)
    public override async ValueTask HandleAsync(InitializeStep initializeRequest, MessageContext messageContext)
    {
        Verify.NotNull(initializeRequest);

        if (initializeRequest.StepInfo.StepTypeInfoCase != ProcessStepInfo.StepTypeInfoOneofCase.Process || initializeRequest.StepInfo.Process?.Steps == null)
        {
            throw new KernelException("The process must be of type 'Process'").Log(this._processLogger);
        }

        // Only initialize once. This check is required as the actor can be re-activated from persisted state and
        // this should not result in multiple initializations.
        if (this._isInitialized)
        {
            return;
        }

        // Initialize the process
        await this.InitializeProcessActorAsync(initializeRequest.StepInfo, initializeRequest.ParentProcessId, initializeRequest.EventProxyStepId).ConfigureAwait(false);

        // Save the state
        //await this.StateManager.AddStateAsync(ActorStateKeys.ProcessInfoState, processInfo).ConfigureAwait(false);
        //await this.StateManager.AddStateAsync(ActorStateKeys.StepParentProcessId, parentProcessId).ConfigureAwait(false);
        //await this.StateManager.AddStateAsync(ActorStateKeys.StepActivatedState, true).ConfigureAwait(false);
        //if (!string.IsNullOrWhiteSpace(eventProxyStepId))
        //{
        //    await this.StateManager.AddStateAsync(ActorStateKeys.EventProxyStepId, eventProxyStepId).ConfigureAwait(false);
        //}
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the process with an initial event and an optional kernel.
    /// </summary>
    /// <param name="keepAlive">Indicates if the process should wait for external events after it's finished processing.</param>
    /// <returns> <see cref="Task"/></returns>
    public Task StartAsync(bool keepAlive)
    {
        if (!this._isInitialized)
        {
            throw new InvalidOperationException("The process cannot be started before it has been initialized.").Log(this._processLogger);
        }

        this._processCancelSource = new CancellationTokenSource();
        this._processTask = this._joinableTaskFactory.RunAsync(()
            => this.Internal_ExecuteAsync(keepAlive: keepAlive, cancellationToken: this._processCancelSource.Token));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the process with an initial event and then waits for the process to finish. In this case the process will not
    /// keep alive waiting for external events after the internal messages have stopped.
    /// </summary>
    /// <param name="processEvent">Required. The <see cref="KernelProcessEvent"/> to start the process with.</param>
    /// <returns>A <see cref="Task"/></returns>
    private async ValueTask RunOnceAsync(string processEvent)
    {
        Verify.NotNull(processEvent, nameof(processEvent));

        AgentId externalEventBufferId = new("ExternalEventBufferAgent", this._id.Key);
        //IExternalEventBuffer externalEventQueue = this.ProxyFactory.CreateActorProxy<IExternalEventBuffer>(new ActorId(this.Id.GetId()), nameof(ExternalEventBufferActor));

        await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = processEvent }, externalEventBufferId).ConfigureAwait(false);
        //await externalEventQueue.EnqueueAsync(processEvent).ConfigureAwait(false);

        await this.StartAsync(keepAlive: false).ConfigureAwait(false);
        await this._processTask!.JoinAsync().ConfigureAwait(false);
    }

    public ValueTask HandleAsync(RunOnce message, MessageContext messageContext)
    {
        return this.RunOnceAsync(message.Event);
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
    /// <returns>A <see cref="Task"/></returns>
    public async Task SendMessageAsync(string processEvent)
    {
        Verify.NotNull(processEvent, nameof(processEvent));
        await this._externalEventChannel.Writer.WriteAsync(processEvent.ToKernelProcessEvent()).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the process information.
    /// </summary>
    /// <returns>An instance of <see cref="KernelProcess"/></returns>
    //public async Task<DaprProcessInfo> GetProcessInfoAsync()
    //{
    //    return await this.ToDaprProcessInfoAsync().ConfigureAwait(false);
    //}

    public async ValueTask<ProcessStepInfo> HandleAsync(ToProcessStepInfo request, MessageContext messageContext)
    {
        var processInfo = await this.ToProcessInfoAsync(messageContext.CancellationToken).ConfigureAwait(false);
        return processInfo;
    }

    /// <summary>
    /// When the process is used as a step within another process, this method will be called
    /// rather than ToKernelProcessAsync when extracting the state.
    /// </summary>
    /// <returns>A <see cref="Task{DaprStepInfo}"/></returns>
    //public override async Task<ProcessStepInfo> HandleAsync(ToProcessStepInfo request)
    //{
    //    return await this.ToProcessInfoAsync().ConfigureAwait(false);
    //}

    //protected override async Task OnActivateAsync()
    //{
    //    var existingProcessInfo = await this.StateManager.TryGetStateAsync<DaprProcessInfo>(ActorStateKeys.ProcessInfoState).ConfigureAwait(false);
    //    if (existingProcessInfo.HasValue)
    //    {
    //        this.ParentProcessId = await this.StateManager.GetStateAsync<string>(ActorStateKeys.StepParentProcessId).ConfigureAwait(false);
    //        string? eventProxyStepId = null;
    //        if (await this.StateManager.ContainsStateAsync(ActorStateKeys.EventProxyStepId).ConfigureAwait(false))
    //        {
    //            eventProxyStepId = await this.StateManager.GetStateAsync<string>(ActorStateKeys.EventProxyStepId).ConfigureAwait(false);
    //        }
    //        await this.InitializeProcessActorAsync(existingProcessInfo.Value, this.ParentProcessId, eventProxyStepId).ConfigureAwait(false);
    //    }
    //}

    /// <summary>
    /// The name of the step.
    /// </summary>
    protected override string Name => this._process?.State.Name ?? throw new KernelException("The Process must be initialized before accessing the Name property.").Log(this._processLogger);

    #endregion

    /// <summary>
    /// Handles a <see cref="ProcessMessage"/> that has been sent to the process. This happens only in the case
    /// of a process (this one) running as a step within another process (this one's parent). In this case the
    /// entire sub-process should be executed within a single superstep.
    /// </summary>
    /// <param name="message">The message to process.</param>
    internal override async Task HandleMessageAsync(ProcessMessageCore message)
    {
        if (string.IsNullOrWhiteSpace(message.TargetEventId))
        {
            throw new KernelException("Internal Process Error: The target event id must be specified when sending a message to a step.").Log(this._processLogger);
        }

        string eventId = message.TargetEventId!;
        if (this._outputEdges!.TryGetValue(eventId, out List<ProcessEdge>? edges) && edges is not null)
        {
            foreach (var edge in edges)
            {
                // Create the external event that will be used to start the nested process. Since this event came
                // from outside this processes, we set the visibility to internal so that it's not emitted back out again.
                KernelProcessEvent nestedEvent = new() { Id = eventId, Data = message.TargetEventData };

                // Run the nested process completely within a single superstep.
                await this.RunOnceAsync(nestedEvent.ToJson()).ConfigureAwait(false);
            }
        }
    }

    internal static AgentId GetScopedGlobalErrorEventBufferId(string processId) => new("", $"{ProcessConstants.GlobalErrorEventId}_{processId}");

    #region Private Methods

    /// <summary>
    /// Initializes this process as a step within another process.
    /// </summary>
    protected override ValueTask ActivateStepAsync()
    {
        // The process does not need any further initialization as it's already been initialized.
        // Override the base method to prevent it from being called.
        return default;
    }

    private async Task InitializeProcessActorAsync(ProcessStepInfo processInfo, string? parentProcessId, string? eventProxyStepId)
    {
        Verify.NotNull(processInfo, nameof(processInfo));
        Verify.NotNull(processInfo.Process.Steps);

        this.ParentProcessId = parentProcessId;
        this._process = processInfo;
        this._stepsInfos = [.. this._process.Process.Steps];
        this._processLogger = this._kernel.LoggerFactory?.CreateLogger(this._process.State.Name) ?? new NullLogger<CoreProcess>();
        if (!string.IsNullOrWhiteSpace(eventProxyStepId))
        {
            this.EventProxyStepId = new AgentId("", eventProxyStepId);
        }

        // Initialize the input and output edges for the process
        this._outputEdges = this._process.Edges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Edges.ToList());

        // Initialize the steps within this process
        foreach (var step in this._stepsInfos)
        {
            // The current step should already have a name.
            Verify.NotNull(step.State?.Name);

            switch (step.StepTypeInfoCase)
            {
                case ProcessStepInfo.StepTypeInfoOneofCase.Process:
                    // The process will only have an Id if its already been executed.
                    if (string.IsNullOrWhiteSpace(step.State.Id))
                    {
                        step.State.Id = Guid.NewGuid().ToString();
                    }

                    // Initialize the step as a process.
                    var initializeMessage = new InitializeStep
                    {
                        StepInfo = step,
                        ParentProcessId = this._id.Key,
                        EventProxyStepId = eventProxyStepId
                    };

                    var scopedProcessId = this.ScopedActorId(new AgentId(nameof(CoreProcess), step.State.Id!));
                    await this._runtime.SendMessageAsync(initializeMessage, scopedProcessId).ConfigureAwait(false);
                    break;

                case ProcessStepInfo.StepTypeInfoOneofCase.Map:
                    // Initialize the step as a map.
                    //ActorId scopedMapId = this.ScopedActorId(new ActorId(mapStep.State.Id!));
                    //IMap mapActor = this.ProxyFactory.CreateActorProxy<IMap>(scopedMapId, nameof(MapActor));
                    //await mapActor.InitializeMapAsync(mapStep, this.Id.GetId()).ConfigureAwait(false);
                    //stepActor = this.ProxyFactory.CreateActorProxy<IStep>(scopedMapId, nameof(MapActor));

                    // TODO: Map initialization is different and needs different parameters.
                    //AgentId scopedMapId = this.ScopedActorId(new AgentId(nameof(CoreMap), step.State.Id!));

                    //var initializeMapMessage = new InitializeStep
                    //{
                    //    StepInfo = step,
                    //    ParentProcessId = this._id.Key,
                    //    EventProxyStepId = eventProxyStepId
                    //};

                    //break;

                default:
                    // The current step should already have an Id.
                    Verify.NotNull(step.State?.Id);

                    var scopedStepId = this.ScopedActorId(new AgentId(nameof(CoreStep), step.State.Id!));
                    var initializeStepMessage = new InitializeStep
                    {
                        StepInfo = step,
                        ParentProcessId = this._id.Key,
                        EventProxyStepId = eventProxyStepId
                    };

                    await this._runtime.SendMessageAsync(initializeStepMessage, scopedStepId).ConfigureAwait(false);

                    break;
            }

            this._steps.Add(step);
        }

        this._isInitialized = true;
    }

    private async Task Internal_ExecuteAsync(int maxSupersteps = 100, bool keepAlive = true, CancellationToken cancellationToken = default)
    {
        try
        {
            // Run the Pregel algorithm until there are no more messages being sent.
            for (int superstep = 0; superstep < maxSupersteps; superstep++)
            {
                // Check for EndStep messages. If there are any then cancel the process.
                if (await this.IsEndMessageSentAsync().ConfigureAwait(false))
                {
                    this._processCancelSource?.Cancel();
                    break;
                }

                // Translate any global error events into an message that targets the appropriate step, when one exists.
                await this.HandleGlobalErrorMessageAsync().ConfigureAwait(false);

                // Check for external events
                await this.EnqueueExternalMessagesAsync().ConfigureAwait(false);

                // Reach out to all of the steps in the process and instruct them to retrieve their pending messages from their associated queues.
                var stepPreparationTasks = this._steps.Select(step => this.PrepareIncomingMessagesAsync(step, cancellationToken)).ToArray();

                // Process the incoming messages for each step.
                var stepProcessingTasks = this._steps.Select(step => this.ProcessIncomingMessagesAsync(step, cancellationToken)).ToArray();
                await Task.WhenAll(stepProcessingTasks).ConfigureAwait(false);

                // Handle public events that need to be bubbled out of the process.
                await this.SendOutgoingPublicEventsAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._processLogger?.LogError(ex, "An error occurred while running the process: {ErrorMessage}.", ex.Message);
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

    private async Task<int> PrepareIncomingMessagesAsync(ProcessStepInfo step, CancellationToken cancellationToken)
    {
        var agentType = step.StepTypeInfoCase switch
        {
            ProcessStepInfo.StepTypeInfoOneofCase.Process => nameof(CoreProcess),
            ProcessStepInfo.StepTypeInfoOneofCase.Map => "TODO: CoreMap", //nameof(CoreMap),
            _ => nameof(CoreStep)
        };

        var scopedStepId = this.ScopedActorId(new AgentId(agentType, step.State.Id!));
        var response = await this._runtime.SendMessageAsync(new PrepareIncomingMessages(), scopedStepId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response is not PrepareIncomingMessagesResponse prepareResponse)
        {
            throw new KernelException("The response from the PrepareIncomingMessages was not of the expected type.").Log(this._processLogger);
        }

        return prepareResponse.MessageCount;
    }

    private async Task ProcessIncomingMessagesAsync(ProcessStepInfo step, CancellationToken cancellationToken)
    {
        var agentType = step.StepTypeInfoCase switch
        {
            ProcessStepInfo.StepTypeInfoOneofCase.Process => nameof(CoreProcess),
            ProcessStepInfo.StepTypeInfoOneofCase.Map => "TODO: CoreMap", //nameof(CoreMap),
            _ => nameof(CoreStep)
        };

        var scopedStepId = this.ScopedActorId(new AgentId(agentType, step.State.Id!));
        await this._runtime.SendMessageAsync(new ProcessIncomingMessages(), scopedStepId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes external events that have been sent to the process, translates them to <see cref="ProcessMessage"/>s, and enqueues
    /// them to the provided message channel so that they can be processed in the next superstep.
    /// </summary>
    private async Task EnqueueExternalMessagesAsync()
    {
        //IExternalEventBuffer externalEventQueue = this.ProxyFactory.CreateActorProxy<IExternalEventBuffer>(new ActorId(this.Id.GetId()), nameof(ExternalEventBufferActor));

        AgentId externalEventBufferId = new(nameof(ExternalEventBufferAgent), this._id.Key);
        var dequeueEventsResult = await this._runtime.SendMessageAsync(new DequeueMessage(), externalEventBufferId).ConfigureAwait(false);
        if (dequeueEventsResult is not DequeueMessageResponse dequeueResponse)
        {
            throw new KernelException("The response from the DequeueExternalEvents was not of the expected type.").Log(this._processLogger);
        }

        IList<string> dequeuedEvents = dequeueResponse.Messages;
        IList<KernelProcessEvent> externalEvents = dequeuedEvents.ToKernelProcessEvents();

        foreach (KernelProcessEvent externalEvent in externalEvents)
        {
            if (this._outputEdges!.TryGetValue(externalEvent.Id!, out List<ProcessEdge>? edges) && edges is not null)
            {
                foreach (ProcessEdge edge in edges)
                {
                    KernelProcessEdge kernelProcessEdge = new(edge.SourceStepId, new KernelProcessFunctionTarget(edge.OutputTarget.StepId, edge.OutputTarget.FunctionName, parameterName: edge.OutputTarget.ParameterName, targetEventId: edge.OutputTarget.TargetEventId));
                    ProcessMessage message = ProcessMessageFactory.CreateFromEdge(kernelProcessEdge, externalEvent.Data);
                    var scopedMessageBufferId = this.ScopedActorId(new AgentId(nameof(MessageBufferAgent), edge.OutputTarget.StepId));
                    //var messageQueue = this.ProxyFactory.CreateActorProxy<IMessageBuffer>(scopedMessageBufferId, nameof(MessageBufferActor));
                    //var messageQueueId = new AgentId(nameof(MessageBufferAgent), scopedMessageBufferId.Key);
                    await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = message.ToJson() }, scopedMessageBufferId).ConfigureAwait(false);
                    //await messageQueue.EnqueueAsync(message.ToJson()).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Check for the presence of an global-error event and any edges defined for processing it.
    /// When both exist, the error event is processed and sent to the appropriate targets.
    /// </summary>
    private async Task HandleGlobalErrorMessageAsync()
    {
        //var errorEventQueue = this.ProxyFactory.CreateActorProxy<IEventBuffer>(ProcessActor.GetScopedGlobalErrorEventBufferId(this.Id.GetId()), nameof(EventBufferActor));
        var errorEventQueueId = new AgentId(nameof(EventBufferAgent), ProcessConstants.GlobalErrorEventId);
        var errorDequeueResult = await this._runtime.SendMessageAsync(new DequeueMessage(), errorEventQueueId).ConfigureAwait(false);
        if (errorDequeueResult is not DequeueMessageResponse errorDequeueResponse)
        {
            throw new KernelException("The response from the DequeueGlobalErrorEvents was not of the expected type.").Log(this._processLogger);
        }

        IList<string> errorEvents = errorDequeueResponse.Messages; //await errorEventQueue.DequeueAllAsync().ConfigureAwait(false);
        if (errorEvents.Count == 0)
        {
            // No error events in queue.
            return;
        }

        var errorEdges = this.GetEdgeForEvent(ProcessConstants.GlobalErrorEventId).ToArray();
        if (errorEdges.Length == 0)
        {
            // No further action is required when there are no targetes defined for processing the error.
            return;
        }

        IList<ProcessEvent> processErrorEvents = errorEvents.ToProcessEvents();
        foreach (var errorEdge in errorEdges)
        {
            foreach (ProcessEvent errorEvent in processErrorEvents)
            {
                KernelProcessEdge kernelProcessEdge = new(errorEdge.SourceStepId, new KernelProcessFunctionTarget(errorEdge.OutputTarget.StepId, errorEdge.OutputTarget.FunctionName));
                var errorMessage = ProcessMessageFactory.CreateFromEdge(kernelProcessEdge, errorEvent.Data);

                var scopedErrorMessageBufferId = this.ScopedActorId(new AgentId(nameof(MessageBufferAgent), errorEdge.OutputTarget.StepId));
                //var errorStepQueue = this.ProxyFactory.CreateActorProxy<IMessageBuffer>(scopedErrorMessageBufferId, nameof(MessageBufferActor));
                //await errorStepQueue.EnqueueAsync(errorMessage.ToJson()).ConfigureAwait(false);
                await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = errorMessage.ToJson() }, scopedErrorMessageBufferId).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Public events that are produced inside of this process need to be sent to the parent process. This method reads
    /// all of the public events from the event buffer and sends them to the targeted step in the parent process.
    /// </summary>
    private async Task SendOutgoingPublicEventsAsync()
    {
        // Loop through all steps that are processes and call a function requesting their outgoing events, then queue them up.
        if (!string.IsNullOrWhiteSpace(this.ParentProcessId))
        {
            // Handle public events that need to be bubbled out of the process.
            //IEventBuffer eventQueue = this.ProxyFactory.CreateActorProxy<IEventBuffer>(new ActorId(this.Id.GetId()), nameof(EventBufferActor));
            AgentId eventBufferId = new AgentId(nameof(EventBufferAgent), this._id.Key);

            //IList<string> allEvents = await eventQueue.DequeueAllAsync().ConfigureAwait(false);
            var dequeueResult = await this._runtime.SendMessageAsync(new DequeueMessage(), eventBufferId).ConfigureAwait(false);
            if (dequeueResult is not DequeueMessageResponse dequeueResponse)
            {
                throw new KernelException("The response from the DequeuePublicEvents was not of the expected type.").Log(this._processLogger);
            }

            IList<string> allEvents = dequeueResponse.Messages;
            IList<ProcessEvent> processEvents = allEvents.ToProcessEvents();

            foreach (ProcessEvent processEvent in processEvents)
            {
                ProcessEvent scopedEvent = this.ScopedEvent(processEvent);
                if (this._outputEdges!.TryGetValue(scopedEvent.QualifiedId, out List<ProcessEdge>? edges) && edges is not null)
                {
                    foreach (var edge in edges)
                    {
                        KernelProcessEdge kernelProcessEdge = new(edge.SourceStepId, new KernelProcessFunctionTarget(edge.OutputTarget.StepId, edge.OutputTarget.FunctionName));
                        ProcessMessage message = ProcessMessageFactory.CreateFromEdge(kernelProcessEdge, scopedEvent.Data);
                        var scopedMessageBufferId = this.ScopedActorId(new AgentId(nameof(EventBufferAgent), edge.OutputTarget.StepId), scopeToParent: true);
                        //var messageQueue = this.ProxyFactory.CreateActorProxy<IMessageBuffer>(scopedMessageBufferId, nameof(MessageBufferActor));
                        //await messageQueue.EnqueueAsync(message.ToJson()).ConfigureAwait(false);
                        await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = message.ToJson() }, scopedMessageBufferId).ConfigureAwait(false);
                    }
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
        var scopedMessageBufferId = this.ScopedActorId(new AgentId(nameof(MessageBufferAgent), ProcessConstants.EndStepName));
        //var endMessageQueue = this.ProxyFactory.CreateActorProxy<IMessageBuffer>(scopedMessageBufferId, nameof(MessageBufferActor));

        //var messages = await endMessageQueue.DequeueAllAsync().ConfigureAwait(false);
        var dequeueResult = await this._runtime.SendMessageAsync(new DequeueMessage(), scopedMessageBufferId).ConfigureAwait(false);
        if (dequeueResult is not DequeueMessageResponse dequeueResponse)
        {
            throw new KernelException("The response from the DequeuePublicEvents was not of the expected type.").Log(this._processLogger);
        }

        return dequeueResponse.Messages.Count > 0;
    }

    /// <summary>
    /// Builds a <see cref="DaprProcessInfo"/> from the current <see cref="ProcessActor"/>.
    /// </summary>
    /// <returns>An instance of <see cref="DaprProcessInfo"/></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<ProcessStepInfo> ToProcessInfoAsync(CancellationToken cancellationToken)
    {
        var processState = new ProcessStepState() { Name = this.Name, Version = this._process!.State.Version, Id = this._id.Key };
        var stepTasks = this._steps.Select(step => this.GetStepAsProcessInfoAsync(step, cancellationToken)).ToList();
        var steps = await Task.WhenAll(stepTasks).ConfigureAwait(false);
        return new ProcessStepInfo { InnerStepDotnetType = this._process!.InnerStepDotnetType, Edges = { this._process!.Edges }, State = processState, Process = new ProcessStep() { Steps = { steps } } };
    }

    private async Task<ProcessStepInfo> GetStepAsProcessInfoAsync(ProcessStepInfo step, CancellationToken cancellationToken)
    {
        var agentType = step.StepTypeInfoCase switch
        {
            ProcessStepInfo.StepTypeInfoOneofCase.Process => nameof(CoreProcess),
            ProcessStepInfo.StepTypeInfoOneofCase.Map => "TODO: CoreMap", //nameof(CoreMap),
            _ => nameof(CoreStep)
        };

        var scopedStepId = this.ScopedActorId(new AgentId(agentType, step.State.Id!));
        var response = await this._runtime.SendMessageAsync(new ToProcessStepInfo(), scopedStepId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response is not ProcessStepInfo stepInfoResponse)
        {
            throw new KernelException("The response from the PrepareIncomingMessages was not of the expected type.").Log(this._processLogger);
        }

        return stepInfoResponse;
    }

    /// <summary>
    /// Scopes the Id of a step within the process to the process.
    /// </summary>
    /// <param name="actorId">The actor Id to scope.</param>
    /// <param name="scopeToParent">Indicates if the Id should be scoped to the parent process.</param>
    /// <returns>A new <see cref="ActorId"/> which is scoped to the process.</returns>
    private AgentId ScopedActorId(AgentId actorId, bool scopeToParent = false)
    {
        if (scopeToParent && string.IsNullOrWhiteSpace(this.ParentProcessId))
        {
            throw new InvalidOperationException("The parent process Id must be set before scoping to the parent process.");
        }

        string id = scopeToParent ? this.ParentProcessId! : this._id.Key;
        return new AgentId(actorId.Type, $"{id}.{actorId.Key}");
    }

    /// <summary>
    /// Generates a scoped event for the step.
    /// </summary>
    /// <param name="daprEvent">The event.</param>
    /// <returns>A <see cref="ProcessEvent"/> with the correctly scoped namespace.</returns>
    private ProcessEvent ScopedEvent(ProcessEvent daprEvent)
    {
        Verify.NotNull(daprEvent);
        return daprEvent with { Namespace = $"{this.Name}_{this._process!.State.Id}" };
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
