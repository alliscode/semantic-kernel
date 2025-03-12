// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using System.Text.Json;
using Google.Protobuf.Collections;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Runtime;
using Microsoft.SemanticKernel.Process.Serialization;
using Process.Runtime.Core;

namespace Microsoft.SemanticKernel;

[TypeSubscription("default")]
internal class CoreStep : BaseAgent, IKernelProcessMessageChannel, IHandle<InitializeStep>, IHandle<PrepareIncomingMessages, PrepareIncomingMessagesResponse>, IHandle<ToProcessStepInfo, ProcessStepInfo>, IHandle<ProcessIncomingMessages>
{
    private readonly Lazy<ValueTask> _activateTask;
    private readonly IAgentRuntime _runtime;
    private readonly AgentId _id;

    private ProcessStepInfo? _stepInfo;
    private ILogger? _logger;
    private Type? _innerStepType;

    private bool _isInitialized;

    protected Kernel _kernel;
    protected string? _eventNamespace;

    internal Queue<ProcessMessageCore> _incomingMessages = new();
    internal KernelProcessStepState? _stepState;
    internal Type? _stepStateType;
    internal Dictionary<string, List<ProcessEdge>>? _outputEdges;
    internal readonly Dictionary<string, KernelFunction> _functions = [];
    internal Dictionary<string, Dictionary<string, object?>?>? _inputs = [];
    internal Dictionary<string, Dictionary<string, object?>?>? _initialInputs = [];

    internal string? ParentProcessId;
    internal AgentId? EventProxyStepId;

    /// <summary>
    /// Represents a step in a process that is running in-process.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="kernel">Required. An instance of <see cref="Kernel"/>.</param>
    /// <summary>
    /// Represents a step in a process that is running in-process.
    /// </summary>
    /// <param name="stepInfo">An instance of <see cref="KernelProcessStepInfo"/></param>
    /// <param name="kernel">Required. An instance of <see cref="Kernel"/>.</param>
    /// <param name="parentProcessId">Optional. The Id of the parent process if one exists.</param>
    public CoreStep(AgentId id, IAgentRuntime runtime, Kernel kernel) : base(id, runtime, "A step agent")
    {
        this._id = id;
        this._kernel = kernel;
        this._runtime = runtime;
        this._activateTask = new Lazy<ValueTask>(this.ActivateStepAsync);
    }

    #region Public Actor Methods

    ///// <summary>
    ///// Initializes the step with the provided step information.
    ///// </summary>
    ///// <param name="stepInfo">The <see cref="KernelProcessStepInfo"/> instance describing the step.</param>
    ///// <param name="parentProcessId">The Id of the parent process if one exists.</param>
    ///// <param name="eventProxyStepId">An optional identifier of an actor requesting to proxy events.</param>
    ///// <returns>A <see cref="ValueTask"/></returns>
    //public async Task InitializeStepAsync(ProcessStepInfo stepInfo, string? parentProcessId, string? eventProxyStepId = null)
    //{
    //    Verify.NotNull(stepInfo, nameof(stepInfo));

    //    // Only initialize once. This check is required as the actor can be re-activated from persisted state and
    //    // this should not result in multiple initializations.
    //    if (this._isInitialized)
    //    {
    //        return;
    //    }

    //    this.InitializeStep(stepInfo, parentProcessId, eventProxyStepId);

    //    // TODO: Save initial state
    //    //await this.StateManager.AddStateAsync(ActorStateKeys.StepInfoState, stepInfo).ConfigureAwait(false);
    //    //await this.StateManager.AddStateAsync(ActorStateKeys.StepParentProcessId, parentProcessId).ConfigureAwait(false);
    //    //if (!string.IsNullOrWhiteSpace(eventProxyStepId))
    //    //{
    //    //    await this.StateManager.AddStateAsync(ActorStateKeys.EventProxyStepId, eventProxyStepId).ConfigureAwait(false);
    //    //}
    //    //await this.StateManager.SaveStateAsync().ConfigureAwait(false);
    //}

    public virtual async ValueTask HandleAsync(InitializeStep initializaMessage, MessageContext messageContext)
    {
        Verify.NotNull(initializaMessage.StepInfo, nameof(initializaMessage.StepInfo));

        // Only initialize once. This check is required as the actor can be re-activated from persisted state and
        // this should not result in multiple initializations.
        if (this._isInitialized)
        {
            return;
        }

        this.InitializeStep(initializaMessage.StepInfo, initializaMessage.ParentProcessId, initializaMessage.EventProxyStepId);
    }

    /// <summary>
    /// Initializes the step with the provided step information.
    /// </summary>
    /// <param name="stepInfo">The <see cref="KernelProcessStepInfo"/> instance describing the step.</param>
    /// <param name="parentProcessId">The Id of the parent process if one exists.</param>
    /// <param name="eventProxyStepId">An optional identifier of an actor requesting to proxy events.</param>
    private void InitializeStep(ProcessStepInfo stepInfo, string? parentProcessId, string? eventProxyStepId = null)
    {
        Verify.NotNull(stepInfo, nameof(stepInfo));

        // Attempt to load the inner step type
        this._innerStepType = Type.GetType(stepInfo.InnerStepDotnetType);
        if (this._innerStepType is null)
        {
            throw new KernelException($"Could not load the inner step type '{stepInfo.InnerStepDotnetType}'.").Log(this._logger);
        }

        this.ParentProcessId = parentProcessId;
        this._stepInfo = stepInfo;
        this._stepState = this.GetKernelProcessStepState(this._stepInfo);
        this._logger = this._kernel.LoggerFactory?.CreateLogger(this._innerStepType) ?? new NullLogger<CoreStep>();
        this._outputEdges = this._stepInfo.Edges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Edges.ToList());
        this._eventNamespace = $"{this._stepInfo.State.Name}_{this._stepInfo.State.Id}";

        if (!string.IsNullOrWhiteSpace(eventProxyStepId))
        {
            this.EventProxyStepId = new AgentId("EventProxy", eventProxyStepId);
        }

        this._isInitialized = true;
    }

    private KernelProcessStepState? GetKernelProcessStepState(ProcessStepInfo stepInfo)
    {
        if (stepInfo.State is null)
        {
            return null;
        }

        KernelProcessStepState? stateObject = null;

        if (Type.GetType(stepInfo.InnerStepDotnetType).TryGetSubtypeOfStatefulStep(out Type? genericStepType) && genericStepType is not null)
        {
            // The step is a subclass of KernelProcessStep<>, so we need to extract the generic type argument
            // and create an instance of the corresponding KernelProcessStepState<>.
            var userStateType = genericStepType.GetGenericArguments()[0];
            Verify.NotNull(userStateType);

            var stateType = typeof(KernelProcessStepState<>).MakeGenericType(userStateType);
            Verify.NotNull(stateType);

            var initialState = JsonSerializer.Deserialize(stepInfo.State.State, userStateType) as KernelProcessStepState;
            stateObject = (KernelProcessStepState?)Activator.CreateInstance(stateType, stepInfo.State.Name, stepInfo.State.Version, stepInfo.State.Id);
            stateType.GetProperty(nameof(KernelProcessStepState<object>.State))?.SetValue(stateObject, initialState);
        }
        else
        {
            stateObject = new KernelProcessStepState(stepInfo.State.Name, stepInfo.State.Version, stepInfo.State.Id);
        }

        return stateObject;
    }

    /// <summary>
    /// Triggers the step to dequeue all pending messages and prepare for processing.
    /// </summary>
    /// <returns>A <see cref="Task{Task}"/> where T is an <see cref="int"/> indicating the number of messages that are prepared for processing.</returns>
    //public async Task<int> PrepareIncomingMessagesAsync()
    public async ValueTask<PrepareIncomingMessagesResponse> HandleAsync(PrepareIncomingMessages item, MessageContext messageContext)
    {
        //IMessageBuffer messageQueue = this.ProxyFactory.CreateActorProxy<IMessageBuffer>(new ActorId(this.Id.GetId()), nameof(MessageBufferActor));

        AgentId messageQueueId = new AgentId(nameof(MessageBufferAgent), this._id.Key);

        // IList<string> incoming = await messageQueue.DequeueAllAsync().ConfigureAwait(false);
        object? incoming = await this._runtime.SendMessageAsync(new DequeueMessage(), messageQueueId).ConfigureAwait(false);
        if (incoming is null || incoming is not DequeueMessageResponse response)
        {
            throw new KernelException("Failed to dequeue messages.").Log(this._logger);
        }

        IList<ProcessMessage> messages = response.Messages.ToProcessMessages();

        foreach (ProcessMessage message in messages)
        {
            MapField<string, ProcessMessageValue> values = new();
            foreach (var kvp in message.Values)
            {
                values.Add(kvp.Key, new ProcessMessageValue() { Type = kvp.Value.GetType().AssemblyQualifiedName, Value = JsonSerializer.Serialize(kvp.Value) });
            }

            var messageCore = new ProcessMessageCore() { DestinationId = message.DestinationId, SourceId = message.SourceId, FunctionName = message.FunctionName, Values = { values } };
            this._incomingMessages.Enqueue(messageCore);
        }

        // Save the incoming messages to state
        // TODO: State
        //await this.StateManager.SetStateAsync(ActorStateKeys.StepIncomingMessagesState, this._incomingMessages).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);

        return new() { MessageCount = this._incomingMessages.Count };
    }

    /// <summary>
    /// Triggers the step to process all prepared messages.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    //public async Task ProcessIncomingMessagesAsync()
    public virtual async ValueTask HandleAsync(ProcessIncomingMessages item, MessageContext messageContext)
    {
        // Handle all the incoming messages one at a time
        while (this._incomingMessages.Count > 0)
        {
            var message = this._incomingMessages.Dequeue();
            await this.HandleMessageAsync(message).ConfigureAwait(false);

            // Save the incoming messages to state
            // TODO: State
            //await this.StateManager.SetStateAsync(ActorStateKeys.StepIncomingMessagesState, this._incomingMessages).ConfigureAwait(false);
            //await this.StateManager.SaveStateAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Extracts the current state of the step and returns it as a <see cref="ProcessStepInfo"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ProcessStepInfo"/></returns>
    public virtual async ValueTask<ProcessStepInfo> HandleAsync(ToProcessStepInfo item, MessageContext messageContext)
    {
        // Lazy one-time initialization of the step before extracting state information.
        // This allows state information to be extracted even if the step has not been activated.
        await this._activateTask.Value.ConfigureAwait(false);

        var stepInfo = new ProcessStepInfo { InnerStepDotnetType = this._stepInfo!.InnerStepDotnetType!, State = this._stepInfo.State, Edges = { this._stepInfo.Edges } };
        return stepInfo;
    }

    /// <summary>
    /// Overrides the base method to initialize the step from persisted state.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    //protected override Task OnActivateAsync()
    //{
    //    //var existingStepInfo = await this.StateManager.TryGetStateAsync<ProcessStepInfo>(ActorStateKeys.StepInfoState).ConfigureAwait(false);
    //    bool? existingStepInfo = false; // TODO: state
    //    if (existingStepInfo.HasValue)
    //    {
    //        // Initialize the step from persisted state
    //        // TODO: State
    //        //string? parentProcessId = await this.StateManager.GetStateAsync<string>(ActorStateKeys.StepParentProcessId).ConfigureAwait(false);
    //        //string? eventProxyStepId = null;
    //        //if (await this.StateManager.ContainsStateAsync(ActorStateKeys.EventProxyStepId).ConfigureAwait(false))
    //        //{
    //        //    eventProxyStepId = await this.StateManager.GetStateAsync<string>(ActorStateKeys.EventProxyStepId).ConfigureAwait(false);
    //        //}
    //        //this.InitializeStep(existingStepInfo.Value, parentProcessId, eventProxyStepId);

    //        //// Load the persisted incoming messages
    //        //var incomingMessages = await this.StateManager.TryGetStateAsync<Queue<ProcessMessage>>(ActorStateKeys.StepIncomingMessagesState).ConfigureAwait(false);
    //        //if (incomingMessages.HasValue)
    //        //{
    //        //    this._incomingMessages = incomingMessages.Value;
    //        //}
    //    }

    //    return Task.CompletedTask;
    //}

    #endregion

    /// <summary>
    /// The name of the step.
    /// </summary>
    protected virtual string Name => this._stepInfo?.State.Name ?? throw new KernelException("The Step must be initialized before accessing the Name property.").Log(this._logger);

    /// <summary>
    /// Emits an event from the step.
    /// </summary>
    /// <param name="processEvent">The event to emit.</param>
    /// <returns>A <see cref="ValueTask"/></returns>
    public ValueTask EmitEventAsync(KernelProcessEvent processEvent) => this.EmitEventAsync(ProcessEvent.Create(processEvent, this._eventNamespace!));

    //internal virtual Task HandleAsync(ProcessMessageCore message)
    //{
    //    return this.HandleAsync(message);
    //}

    /// <summary>
    /// Handles a <see cref="ProcessMessage"/> that has been sent to the step.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <returns>A <see cref="Task"/></returns>
    /// <exception cref="KernelException"></exception>
    internal virtual async Task HandleMessageAsync(ProcessMessageCore message)
    {
        Verify.NotNull(message, nameof(message));

        // Lazy one-time initialization of the step before processing a message
        await this._activateTask.Value.ConfigureAwait(false);

        if (this._functions is null || this._inputs is null || this._initialInputs is null)
        {
            throw new KernelException("The step has not been initialized.").Log(this._logger);
        }

        string messageLogParameters = string.Join(", ", message.Values.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        this._logger?.LogDebug("Received message from '{SourceId}' targeting function '{FunctionName}' and parameters '{Parameters}'.", message.SourceId, message.FunctionName, messageLogParameters);

        // Add the message values to the inputs for the function
        foreach (var kvp in message.Values)
        {
            if (this._inputs.TryGetValue(message.FunctionName, out Dictionary<string, object?>? functionName) && functionName != null && functionName.TryGetValue(kvp.Key, out object? parameterName) && parameterName != null)
            {
                this._logger?.LogWarning("Step {StepName} already has input for {FunctionName}.{Key}, it is being overwritten with a message from Step named '{SourceId}'.", this.Name, message.FunctionName, kvp.Key, message.SourceId);
            }

            if (!this._inputs.TryGetValue(message.FunctionName, out Dictionary<string, object?>? functionParameters))
            {
                this._inputs[message.FunctionName] = [];
                functionParameters = this._inputs[message.FunctionName];
            }

            functionParameters![kvp.Key] = kvp.Value;
        }

        // If we're still waiting for inputs on all of our functions then don't do anything.
        List<string> invocableFunctions = this._inputs.Where(i => i.Value != null && i.Value.All(v => v.Value != null)).Select(i => i.Key).ToList();
        var missingKeys = this._inputs.Where(i => i.Value is null || i.Value.Any(v => v.Value is null));

        if (invocableFunctions.Count == 0)
        {
            string missingKeysLog() => string.Join(", ", missingKeys.Select(k => $"{k.Key}: {string.Join(", ", k.Value?.Where(v => v.Value == null).Select(v => v.Key) ?? [])}"));
            this._logger?.LogInformation("No invocable functions, missing keys: {MissingKeys}", missingKeysLog());
            return;
        }

        // A message can only target one function and should not result in a different function being invoked.
        var targetFunction = invocableFunctions.FirstOrDefault((name) => name == message.FunctionName) ??
            throw new InvalidOperationException($"A message targeting function '{message.FunctionName}' has resulted in a function named '{invocableFunctions.First()}' becoming invocable. Are the function names configured correctly?").Log(this._logger);

        this._logger?.LogInformation("Step with Id `{StepId}` received all required input for function [{TargetFunction}] and is executing.", this.Name, targetFunction);

        // Concat all the inputs and run the function
        KernelArguments arguments = new(this._inputs[targetFunction]!);
        if (!this._functions.TryGetValue(targetFunction, out KernelFunction? function) || function == null)
        {
            throw new InvalidOperationException($"Function {targetFunction} not found in plugin {this.Name}").Log(this._logger);
        }

        // Invoke the function, catching all exceptions that it may throw, and then post the appropriate event.
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            this?._logger?.LogInformation("Invoking function {FunctionName} with arguments {Arguments}", targetFunction, arguments);
            FunctionResult invokeResult = await this.InvokeFunction(function, this._kernel, arguments).ConfigureAwait(false);

            this?._logger?.LogInformation("Function {FunctionName} returned {Result}", targetFunction, invokeResult);

            // Persist the state after the function has been executed
            var stateJson = JsonSerializer.Serialize(this._stepState, this._stepStateType!);

            // TODO: State
            //await this.StateManager.SetStateAsync(ActorStateKeys.StepStateJson, stateJson).ConfigureAwait(false);
            //await this.StateManager.SaveStateAsync().ConfigureAwait(false);

            await this.EmitEventAsync(
                new ProcessEvent
                {
                    Namespace = this._eventNamespace!,
                    SourceId = $"{targetFunction}.OnResult",
                    Data = invokeResult.GetValue<object>()
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger?.LogError(ex, "Error in Step {StepName}: {ErrorMessage}", this.Name, ex.Message);
            await this.EmitEventAsync(
                new ProcessEvent
                {
                    Namespace = this._eventNamespace!,
                    SourceId = $"{targetFunction}.OnError",
                    Data = KernelProcessError.FromException(ex),
                    IsError = true
                }).ConfigureAwait(false);
        }
        finally
        {
            // Reset the inputs for the function that was just executed
            this._inputs[targetFunction] = new(this._initialInputs[targetFunction] ?? []);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Initializes the step with the provided step information.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/></returns>
    /// <exception cref="KernelException"></exception>
    protected virtual async ValueTask ActivateStepAsync()
    {
        if (this._stepInfo is null)
        {
            throw new KernelException("A step cannot be activated before it has been initialized.").Log(this._logger);
        }

        // Instantiate an instance of the inner step object
        KernelProcessStep stepInstance = (KernelProcessStep)ActivatorUtilities.CreateInstance(this._kernel.Services, this._innerStepType!);
        var kernelPlugin = KernelPluginFactory.CreateFromObject(stepInstance, pluginName: this._stepInfo.State.Name);

        // Load the kernel functions
        foreach (KernelFunction f in kernelPlugin)
        {
            this._functions.Add(f.Name, f);
        }

        // Creating external process channel actor to be used for external messaging by some steps
        IExternalKernelProcessMessageChannel? externalMessageChannelActor = null;
        var scopedExternalMessageBufferId = this.ScopedActorId(new AgentId(nameof(ExternalMessageBufferAgent), this._id.Key));
        //var actor = this.ProxyFactory.CreateActorProxy<IExternalMessageBuffer>(scopedExternalMessageBufferId, nameof(ExternalMessageBufferActor));
        //externalMessageChannelActor = new ExternalMessageBufferActorWrapper(actor); TODO: Fix external channel stuff


        // Initialize the input channels
        // TODO: Issue #10328 Cloud Events - new Step type dedicated to work as Proxy Step abstraction https://github.com/microsoft/semantic-kernel/issues/10328
        this._initialInputs = this.FindInputChannels(this._functions, this._logger, externalMessageChannelActor);
        this._inputs = this._initialInputs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

        // Activate the step with user-defined state if needed
        KernelProcessStepState? stateObject = null;
        Type? stateType = null;

        // Check if the state has already been persisted
        bool? stepStateType = null; // TODO: State
        // var stepStateType = await this.StateManager.TryGetStateAsync<string>(ActorStateKeys.StepStateType).ConfigureAwait(false);
        if (stepStateType.HasValue)
        {
            // TODO: State
            //stateType = Type.GetType(stepStateType.Value);
            //var stateObjectJson = await this.StateManager.GetStateAsync<string>(ActorStateKeys.StepStateJson).ConfigureAwait(false);
            //stateObject = JsonSerializer.Deserialize(stateObjectJson, stateType!) as ProcessStepState;
        }
        else
        {
            stateType = this._innerStepType.ExtractStateType(out Type? userStateType, this._logger);
            stateObject = this._stepInfo.State.ToKernelProcessStepState(this._innerStepType);

            //stateObject = JsonSerializer.Deserialize(this._stepInfo.State.State, stateType!) as KernelProcessStepState;

            // Persist the state type and type object. TODO: State
            //await this.StateManager.AddStateAsync(ActorStateKeys.StepStateType, stateType.AssemblyQualifiedName).ConfigureAwait(false);
            //await this.StateManager.AddStateAsync(ActorStateKeys.StepStateJson, JsonSerializer.Serialize(stateObject)).ConfigureAwait(false);
            //await this.StateManager.SaveStateAsync().ConfigureAwait(false);
        }

        if (stateType is null || stateObject is null)
        {
            throw new KernelException("The state object for the KernelProcessStep could not be created.").Log(this._logger);
        }

        MethodInfo? methodInfo =
            this._innerStepType!.GetMethod(nameof(KernelProcessStep.ActivateAsync), [stateType]) ??
            throw new KernelException("The ActivateAsync method for the KernelProcessStep could not be found.").Log(this._logger);

        this._stepState = stateObject;
        this._stepStateType = stateType;

        ValueTask activateTask =
            (ValueTask?)methodInfo.Invoke(stepInstance, [stateObject]) ??
            throw new KernelException("The ActivateAsync method failed to complete.").Log(this._logger);

        await stepInstance.ActivateAsync(stateObject).ConfigureAwait(false);
        await activateTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes the provides function with the provided kernel and arguments.
    /// </summary>
    /// <param name="function">The function to invoke.</param>
    /// <param name="kernel">The kernel to use for invocation.</param>
    /// <param name="arguments">The arguments to invoke with.</param>
    /// <returns>A <see cref="Task"/> containing the result of the function invocation.</returns>
    private Task<FunctionResult> InvokeFunction(KernelFunction function, Kernel kernel, KernelArguments arguments)
    {
        return kernel.InvokeAsync(function, arguments: arguments);
    }

    /// <summary>
    /// Emits an event from the step.
    /// </summary>
    /// <param name="daprEvent">The event to emit.</param>
    internal async ValueTask EmitEventAsync(ProcessEvent daprEvent)
    {
        // Emit the event out of the process (this one) if it's visibility is public.
        if (daprEvent.Visibility == KernelProcessEventVisibility.Public)
        {
            if (this.ParentProcessId is not null)
            {
                // Emit the event to the parent process
                //IEventBuffer parentProcess = this.ProxyFactory.CreateActorProxy<IEventBuffer>(new ActorId(this.ParentProcessId), nameof(EventBufferActor));
                //await parentProcess.EnqueueAsync(daprEvent.ToJson()).ConfigureAwait(false);

                AgentId parentProcessId = new(nameof(EventBufferAgent), this.ParentProcessId);
                await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = daprEvent.ToJson() }, parentProcessId).ConfigureAwait(false);
            }
        }

        if (this.EventProxyStepId.HasValue)
        {
            //IEventBuffer proxyBuffer = this.ProxyFactory.CreateActorProxy<IEventBuffer>(this.EventProxyStepId, nameof(EventBufferActor));
            //await proxyBuffer.EnqueueAsync(daprEvent.ToJson()).ConfigureAwait(false);

            AgentId proxyBufferId = new(nameof(EventBufferAgent), this.EventProxyStepId.Value.Key);
            await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = daprEvent.ToJson() }, proxyBufferId).ConfigureAwait(false);
        }

        // Get the edges for the event and queue up the messages to be sent to the next steps.
        bool foundEdge = false;
        foreach (ProcessEdge edge in this.GetEdgeForEvent(daprEvent.QualifiedId))
        {
            KernelProcessEdge kernelProcessEdge = new(edge.SourceStepId, new KernelProcessFunctionTarget(edge.OutputTarget.StepId, edge.OutputTarget.FunctionName));

            ProcessMessage message = ProcessMessageFactory.CreateFromEdge(kernelProcessEdge, daprEvent.Data);
            //ActorId scopedStepId = this.ScopedActorId(new ActorId(edge.OutputTarget.StepId));
            //IMessageBuffer targetStep = this.ProxyFactory.CreateActorProxy<IMessageBuffer>(scopedStepId, nameof(MessageBufferActor));
            //await targetStep.EnqueueAsync(message.ToJson()).ConfigureAwait(false);

            AgentId scopedStepId = this.ScopedActorId(new AgentId(nameof(MessageBufferAgent), edge.OutputTarget.StepId));
            await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = message.ToJson() }, scopedStepId).ConfigureAwait(false);

            foundEdge = true;
        }

        // Error event was raised with no edge to handle it, send it to the global error event buffer.
        if (!foundEdge && daprEvent.IsError && this.ParentProcessId != null)
        {
            //IEventBuffer parentProcess1 = this.ProxyFactory.CreateActorProxy<IEventBuffer>(ProcessActor.GetScopedGlobalErrorEventBufferId(this.ParentProcessId), nameof(EventBufferActor));
            //await parentProcess1.EnqueueAsync(daprEvent.ToJson()).ConfigureAwait(false);

            AgentId parentProcessId = new AgentId(nameof(EventBufferAgent), $"{ProcessConstants.GlobalErrorEventId}_{this.ParentProcessId}");
            await this._runtime.SendMessageAsync(new EnqueueMessage() { Content = daprEvent.ToJson() }, parentProcessId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Scopes the Id of a step within the process to the process.
    /// </summary>
    /// <param name="agentId">The actor Id to scope.</param>
    /// <returns>A new <see cref="AgentId"/> which is scoped to the process.</returns>
    private AgentId ScopedActorId(AgentId agentId)
    {
        return new AgentId(agentId.Type, $"{this.ParentProcessId}.{agentId.Key}");
    }

    /// <summary>
    /// Retrieves all edges that are associated with the provided event Id.
    /// </summary>
    /// <param name="eventId">The event Id of interest.</param>
    /// <returns>A <see cref="IEnumerable{T}"/> where T is <see cref="ProcessEdge"/></returns>
    internal IEnumerable<ProcessEdge> GetEdgeForEvent(string eventId)
    {
        if (this._outputEdges is null)
        {
            return [];
        }

        if (this._outputEdges.TryGetValue(eventId, out List<ProcessEdge>? edges) && edges is not null)
        {
            return edges;
        }

        return [];
    }
}
