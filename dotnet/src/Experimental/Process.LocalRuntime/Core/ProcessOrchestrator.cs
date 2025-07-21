// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Runtime;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;

/// <summary>
/// Simplified process orchestrator that manages step execution using the Pregel algorithm.
/// </summary>
internal sealed class ProcessOrchestrator : IAsyncDisposable
{
    private readonly KernelProcess _processDefinition;
    private readonly ProcessContext _context;
    private readonly IMessageBus _messageBus;
    private readonly StepRegistry _stepRegistry;
    private readonly ILogger _logger;
    private readonly ProcessStateManager? _processStateManager;

    private bool _isInitialized = false;

    /// <summary>
    /// Initializes a new instance of the ProcessOrchestrator.
    /// </summary>
    /// <param name="processDefinition">The process definition to execute.</param>
    /// <param name="context">The process context.</param>
    public ProcessOrchestrator(KernelProcess processDefinition, ProcessContext context)
    {
        Verify.NotNull(processDefinition);
        Verify.NotNull(context);

        this._processDefinition = processDefinition;
        this._context = context;
        this._messageBus = new MessageBus(processDefinition.Edges, processDefinition.Steps, context.ProcessId);
        this._stepRegistry = new StepRegistry();
        this._logger = context.Kernel.LoggerFactory?.CreateLogger<ProcessOrchestrator>() ?? new NullLogger<ProcessOrchestrator>();

        // Log process information for debugging
        this._logger.LogDebug("ProcessOrchestrator: Process={ProcessId}, Steps={StepCount}, Edges={EdgeCount}", processDefinition.State.StepId, processDefinition.Steps?.Count ?? 0, processDefinition.Edges?.Count ?? 0);

        // Debug: Check for edge groups in steps
        if (processDefinition.Steps != null)
        {
            foreach (var step in processDefinition.Steps)
            {
                if (step.IncomingEdgeGroups?.Count > 0)
                {
                    this._logger.LogDebug("Step {StepId} has {EdgeGroupCount} edge groups", step.State.StepId, step.IncomingEdgeGroups.Count);
                    foreach (var group in step.IncomingEdgeGroups)
                    {
                        this._logger.LogDebug("EdgeGroup {GroupId} with {MessageSourceCount} sources", group.Key, group.Value.MessageSources?.Count ?? 0);
                    }
                }
            }
        }

        // Debug: Check for edges with GroupIds
        if (processDefinition.Edges != null)
        {
            foreach (var edgeList in processDefinition.Edges.Values)
            {
                foreach (var edge in edgeList)
                {
                    if (!string.IsNullOrEmpty(edge.GroupId))
                    {
                        this._logger.LogDebug("Edge with GroupId {GroupId} from {SourceStepId}", edge.GroupId, edge.SourceStepId);
                    }
                }
            }
        }

        // Wire the message bus to the context
        this._context.MessageBus = this._messageBus;

        // Initialize process state manager for user state handling
        this._processStateManager = processDefinition.UserStateType != null
            ? new ProcessStateManager(processDefinition.UserStateType, null)
            : null;
    }

    /// <summary>
    /// Executes the process once with the given initial event.
    /// </summary>
    /// <param name="initialEvent">The event to start the process with.</param>
    /// <param name="maxSupersteps">Maximum number of supersteps to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the process execution.</returns>
    public async Task ExecuteOnceAsync(
        KernelProcessEvent initialEvent,
        int maxSupersteps = 100,
        CancellationToken cancellationToken = default)
    {
        await this.EnsureInitializedAsync().ConfigureAwait(false);

        try
        {
            // Add initial event to message bus
            await this._messageBus.AddExternalEventAsync(initialEvent, this._context).ConfigureAwait(false);

            // Add OnEnter events
            await this.EnqueueOnEnterEventsAsync().ConfigureAwait(false);

            // Execute Pregel algorithm
            int consecutiveEmptySupersteps = 0;
            const int maxEmptySupersteps = 5; // Allow for delayed operations
            const int emptySuperstepDelayMs = 200; // Wait between empty supersteps

            for (int superstep = 0; superstep < maxSupersteps && !cancellationToken.IsCancellationRequested; superstep++)
            {
                var pendingMessages = this._messageBus.GetPendingMessages();

                if (pendingMessages.Count == 0)
                {
                    consecutiveEmptySupersteps++;
                    if (consecutiveEmptySupersteps >= maxEmptySupersteps)
                    {
                        break; // No more messages to process after waiting
                    }

                    // Wait a bit for delayed operations to produce messages
                    await Task.Delay(emptySuperstepDelayMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Reset empty superstep counter when we have messages
                consecutiveEmptySupersteps = 0;

                // Execute all messages in parallel
                var executionTasks = pendingMessages.Select(message =>
                    this.ExecuteMessageAsync(message)).ToArray();

                await Task.WhenAll(executionTasks).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error occurred during process execution.");
            throw;
        }
    }

    /// <summary>
    /// Executes the process continuously, waiting for external events.
    /// </summary>
    /// <param name="initialEvent">The initial event to start with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the continuous process execution.</returns>
    public async Task ExecuteContinuouslyAsync(
        KernelProcessEvent initialEvent,
        CancellationToken cancellationToken = default)
    {
        await this.EnsureInitializedAsync().ConfigureAwait(false);

        try
        {
            // Add initial event
            await this._messageBus.AddExternalEventAsync(initialEvent, this._context).ConfigureAwait(false);

            // Add OnEnter events
            await this.EnqueueOnEnterEventsAsync().ConfigureAwait(false);

            // Continuous execution loop
            while (!cancellationToken.IsCancellationRequested)
            {
                var pendingMessages = this._messageBus.GetPendingMessages();

                if (pendingMessages.Count == 0)
                {
                    // Wait for external events or cancellation
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Check for end condition
                if (pendingMessages.Any(m => m.DestinationId.Equals(ProcessConstants.EndStepName, StringComparison.OrdinalIgnoreCase)))
                {
                    break;
                }

                // Execute all messages
                var executionTasks = pendingMessages.Select(message =>
                    this.ExecuteMessageAsync(message)).ToArray();

                await Task.WhenAll(executionTasks).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error occurred during continuous process execution.");
            throw;
        }
    }

    /// <summary>
    /// Sends an external message to the process.
    /// </summary>
    /// <param name="processEvent">The event to send.</param>
    /// <returns>A task representing the message sending.</returns>
    public async Task SendMessageAsync(KernelProcessEvent processEvent)
    {
        await this._messageBus.AddExternalEventAsync(processEvent, this._context).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current process state.
    /// </summary>
    /// <returns>The current KernelProcess representation.</returns>
    public async Task<KernelProcess> GetProcessInfoAsync()
    {
        await this.EnsureInitializedAsync().ConfigureAwait(false);

        var stepInfos = await Task.WhenAll(
            this._stepRegistry.GetAllSteps().Select(step => this.GetStepInfoAsync(step))
        ).ConfigureAwait(false);

        var processState = new KernelProcessState(
            this._processDefinition.State.StepId!,
            this._processDefinition.State.Version,
            this._context.ProcessId);

        var edgesDictionary = this._processDefinition.Edges?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList());

        return new KernelProcess(
            processState,
            stepInfos,
            edgesDictionary,
            this._processDefinition.Threads);
    }

    private async Task EnsureInitializedAsync()
    {
        if (this._isInitialized)
        {
            return;
        }

        await this.InitializeStepsAsync().ConfigureAwait(false);
        this._isInitialized = true;
    }

    private async Task InitializeStepsAsync()
    {
        foreach (var stepInfo in this._processDefinition.Steps ?? [])
        {
            // Use deterministic step ID based on process ID and step name for storage continuity across runs
            var stepId = string.IsNullOrWhiteSpace(stepInfo.State.RunId)
                ? $"{this._context.ProcessId}_{stepInfo.State.StepId}"
                : stepInfo.State.RunId;

            var clonedStepInfo = stepInfo.CloneWithIdAndEdges(stepId, this._logger);

            IProcessStep step = clonedStepInfo switch
            {
                KernelProcess processStep => new ProcessStepWrapper(processStep, this._context),
                KernelProcessMap mapStep => new MapStep(mapStep, this._context),
                KernelProcessProxy proxyStep => new ProxyStep(proxyStep, this._context),
                KernelProcessAgentStep agentStep => new AgentStep(agentStep, this._context, this._processStateManager),
                _ => new FunctionStep(clonedStepInfo, this._context)
            };

            this._stepRegistry.RegisterStep(step);

            // Register any edge groups from the step with the message bus
            if (clonedStepInfo.IncomingEdgeGroups != null)
            {
                this._logger.LogDebug("Step {StepId} has {Count} incoming edge groups", clonedStepInfo.State.StepId, clonedStepInfo.IncomingEdgeGroups.Count);
                foreach (var edgeGroup in clonedStepInfo.IncomingEdgeGroups.Values)
                {
                    this._logger.LogDebug("Registering edge group {GroupId} with {MessageSourceCount} message sources", edgeGroup.GroupId, edgeGroup.MessageSources?.Count ?? 0);
                    this._messageBus.RegisterEdgeGroup(edgeGroup);
                }
            }
        }

        // Save initial state
        if (this._context.StorageManager != null)
        {
            await this.SaveProcessStateAsync().ConfigureAwait(false);
        }
    }

    private async Task EnqueueOnEnterEventsAsync()
    {
        var onEnterEvents = this._processDefinition.Edges
            .Where(kvp => kvp.Key.EndsWith(ProcessConstants.Declarative.OnEnterEvent, StringComparison.OrdinalIgnoreCase));

        foreach (var kvp in onEnterEvents)
        {
            var processEvent = ProcessEvent.Create(
                null,
                this._context.ProcessId,
                this._processDefinition.State.RunId!,
                KernelProcessEventVisibility.Internal);

            await this._messageBus.EmitEventAsync(processEvent, this._context).ConfigureAwait(false);
        }
    }

    private async Task ExecuteMessageAsync(StepMessage message)
    {
        try
        {
            var step = this._stepRegistry.GetStep(message.DestinationId);
            if (step == null)
            {
                this._logger.LogWarning("Step with ID '{StepId}' not found.", message.DestinationId);
                return;
            }

            await step.ExecuteAsync(message, this._context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error executing step '{StepId}' with message from '{SourceId}'.",
                message.DestinationId, message.SourceId);
            throw;
        }
    }

    private async Task<KernelProcessStepInfo> GetStepInfoAsync(IProcessStep step)
    {
        return step switch
        {
            ProcessStepWrapper wrapper => await wrapper.GetStepInfoAsync().ConfigureAwait(false),
            FunctionStep functionStep => await functionStep.GetStepInfoAsync().ConfigureAwait(false),
            MapStep mapStep => throw new NotImplementedException("MapStep.GetStepInfo not yet implemented"),
            ProxyStep proxyStep => throw new NotImplementedException("ProxyStep.GetStepInfo not yet implemented"),
            AgentStep agentStep => throw new NotImplementedException("AgentStep.GetStepInfo not yet implemented"),
            _ => throw new NotImplementedException($"GetStepInfo not implemented for step type: {step.GetType()}")
        };
    }

    private async Task SaveProcessStateAsync()
    {
        if (this._context.StorageManager == null)
        {
            return;
        }

        try
        {
            // Don't call GetProcessInfoAsync during initialization to avoid recursion
            // Instead, get step info directly when initialized
            KernelProcess processInfo;
            if (this._isInitialized)
            {
                processInfo = await this.GetProcessInfoAsync().ConfigureAwait(false);
            }
            else
            {
                // During initialization, create minimal process info without step details
                var processState = new KernelProcessState(
                    this._processDefinition.State.StepId!,
                    this._processDefinition.State.Version,
                    this._context.ProcessId);

                processInfo = new KernelProcess(
                    processState,
                    this._processDefinition.Steps ?? [],
                    this._processDefinition.Edges?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList()),
                    this._processDefinition.Threads);
            }
            var storageKey = (this._processDefinition.State.StepId!, this._context.ProcessId);
            await this._context.StorageManager.SaveProcessDataAsync(storageKey.Item1, storageKey.Item2, processInfo).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to save process state.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this._context.StorageManager != null)
        {
            await this._context.StorageManager.CloseAsync().ConfigureAwait(false);
        }

        // Dispose all steps
        foreach (var step in this._stepRegistry.GetAllSteps().OfType<IAsyncDisposable>())
        {
            await step.DisposeAsync().ConfigureAwait(false);
        }
    }
}
