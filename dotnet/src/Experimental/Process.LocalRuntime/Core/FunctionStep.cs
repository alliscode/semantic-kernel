// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Runtime;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


/// <summary>
/// Standard function step implementation for executing KernelFunction-based steps.
/// </summary>
internal sealed class FunctionStep : IProcessStep
{
    private readonly KernelProcessStepInfo _stepInfo;
    private readonly ProcessContext _context;
    private readonly Dictionary<string, KernelFunction> _functions = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _inputChannels = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _currentInputs = new();
    private readonly Dictionary<string, EdgeGroupProcessor> _edgeGroupProcessors = new();
    private readonly ILogger _logger;

    private KernelProcessStep? _stepInstance;
    private KernelProcessStepState? _stepState;
    private bool _isInitialized = false;

    public FunctionStep(KernelProcessStepInfo stepInfo, ProcessContext context)
    {
        this._stepInfo = stepInfo ?? throw new ArgumentNullException(nameof(stepInfo));
        this._context = context ?? throw new ArgumentNullException(nameof(context));
        this._logger = context.Kernel.LoggerFactory?.CreateLogger<FunctionStep>() ?? new NullLogger<FunctionStep>();
    }

    public string Id => this._stepInfo.State.RunId!;
    public string Name => this._stepInfo.State.StepId!;

    public async Task ExecuteAsync(StepMessage message, ProcessContext context)
    {
        Console.WriteLine($"[FunctionStep] {this.Name} ExecuteAsync called with message: FunctionName={message.FunctionName}, SourceId={message.SourceId}, DestinationId={message.DestinationId}");

        await this.EnsureInitializedAsync().ConfigureAwait(false);

        try
        {
            // Handle AllOf edge group messages
            if (!string.IsNullOrEmpty(message.GroupId))
            {
                Console.WriteLine($"[FunctionStep] {this.Name} received AllOf message: GroupId={message.GroupId}, Source={message.SourceId}, Event={message.SourceEventId}");
                await this.HandleAllOfMessageAsync(message, context).ConfigureAwait(false);
                return;
            }

            Console.WriteLine($"[FunctionStep] {this.Name} Processing regular message for function: {message.FunctionName}");

            // Assign message parameters to input channels
            this.AssignMessageParameters(message);

            // Check if we can execute any functions
            var executableFunctions = this.GetExecutableFunctions();
            Console.WriteLine($"[FunctionStep] {this.Name} Executable functions: [{string.Join(", ", executableFunctions)}], looking for: {message.FunctionName}");

            if (executableFunctions.Count == 0)
            {
                Console.WriteLine($"[FunctionStep] {this.Name} No executable functions available - all function parameters not satisfied yet");
                this._logger.LogDebug("No executable functions available for step '{StepName}'", this.Name);
                return;
            }

            // Execute the target function
            var targetFunction = executableFunctions.FirstOrDefault(f => f == message.FunctionName);
            if (targetFunction == null)
            {
                throw new InvalidOperationException($"Function '{message.FunctionName}' is not executable or does not exist.");
            }

            await this.ExecuteFunctionAsync(targetFunction).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error executing step '{StepName}'", this.Name);

            // Emit error event
            var errorEvent = ProcessEvent.Create(
                KernelProcessError.FromException(ex),
                context.ProcessId,
                $"{this.Name}.{message.FunctionName}.OnError",
                KernelProcessEventVisibility.Public,
                isError: true);

            await context.MessageBus!.EmitEventAsync(errorEvent, context).ConfigureAwait(false);
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (this._isInitialized)
        {
            return;
        }

        // Create step instance
        this._stepInstance = (KernelProcessStep)ActivatorUtilities.CreateInstance(
            this._context.Kernel.Services, this._stepInfo.InnerStepType);

        typeof(KernelProcessStep).GetProperty(nameof(KernelProcessStep.StepName))?
            .SetValue(this._stepInstance, this._stepInfo.State.StepId);

        // Load functions
        var plugin = KernelPluginFactory.CreateFromObject(this._stepInstance, pluginName: this._stepInfo.State.StepId);
        foreach (var function in plugin)
        {
            this._functions.Add(function.Name, function);
        }

        // Initialize input channels
        this.InitializeInputChannels();

        // Initialize edge group processors
        await this.InitializeEdgeGroupProcessorsAsync().ConfigureAwait(false);

        // Load cached state if available
        await this.LoadStepStateAsync().ConfigureAwait(false);

        // Activate step
        await this.ActivateStepAsync().ConfigureAwait(false);

        this._isInitialized = true;
    }

    private void InitializeInputChannels()
    {
        foreach (var function in this._functions.Values)
        {
            var channelDict = new Dictionary<string, object?>();
            var inputDict = new Dictionary<string, object?>();

            foreach (var parameter in function.Metadata.Parameters)
            {
                // Exclude context parameters - these will be injected automatically
                if (IsContextParameter(parameter.ParameterType))
                {
                    Console.WriteLine($"[FunctionStep] {this.Name} Excluding context parameter '{parameter.Name}' of type '{parameter.ParameterType}' from required inputs");
                    continue;
                }

                channelDict[parameter.Name] = null;
                inputDict[parameter.Name] = null;
            }

            this._inputChannels[function.Name] = channelDict;
            this._currentInputs[function.Name] = inputDict;
        }
    }

    /// <summary>
    /// Determines if a parameter type is a context parameter that should be automatically injected.
    /// </summary>
    private static bool IsContextParameter(Type? parameterType)
    {
        return parameterType == typeof(KernelProcessStepContext) ||
               parameterType == typeof(KernelProcessStepExternalContext);
    }

    /// <summary>
    /// Creates a context instance for automatic injection.
    /// </summary>
    private object? CreateContextInstance(Type contextType)
    {
        if (contextType == typeof(KernelProcessStepContext))
        {
            // Create a message channel adapter and pass it to the KernelProcessStepContext constructor
            var messageChannel = new StepMessageChannelAdapter(this.Name, this._context);
            return new KernelProcessStepContext(messageChannel);
        }
        else if (contextType == typeof(KernelProcessStepExternalContext))
        {
            // For external context, we'd need to implement differently
            // For now, return null (not implemented)
            Console.WriteLine($"[FunctionStep] {this.Name} KernelProcessStepExternalContext injection not yet implemented");
            return null;
        }

        return null;
    }

    private async Task LoadStepStateAsync()
    {
        if (this._context.StorageManager == null)
        {
            this._stepState = this._stepInfo.State;
            return;
        }

        try
        {
            var storageKey = (this._stepInfo.State.StepId, this._stepInfo.State.RunId!);
            var storedState = await this._context.StorageManager.GetStepDataAsync(storageKey.Item1, storageKey.Item2).ConfigureAwait(false);

            // For now, we'll just use the default state until we implement proper conversion
            this._stepState = this._stepInfo.State;
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Failed to load step state, using default state");
            this._stepState = this._stepInfo.State;
        }
    }

    private async Task ActivateStepAsync()
    {
        if (this._stepInstance == null || this._stepState == null)
        {
            return;
        }

        var stateType = this._stepInfo.InnerStepType.ExtractStateType(out var userStateType, this._logger);

        this._stepState.InitializeUserState(stateType, userStateType);

        var activateMethod = this._stepInfo.InnerStepType.GetMethod(nameof(KernelProcessStep.ActivateAsync), [stateType]);
        if (activateMethod != null)
        {
            var activateTask = (ValueTask?)activateMethod.Invoke(this._stepInstance, [this._stepState]);
            if (activateTask.HasValue)
            {
                await activateTask.Value.ConfigureAwait(false);
            }
        }
    }

    private void AssignMessageParameters(StepMessage message)
    {
        Console.WriteLine($"[FunctionStep] {this.Name} AssignMessageParameters: Function={message.FunctionName}, MessageData='{message.Data}', Parameters=[{string.Join(", ", message.Parameters.Select(p => $"{p.Key}={p.Value}"))}]");

        if (!this._currentInputs.TryGetValue(message.FunctionName, out var functionInputs))
        {
            Console.WriteLine($"[FunctionStep] {this.Name} Function '{message.FunctionName}' not found in _currentInputs. Available functions: [{string.Join(", ", this._currentInputs.Keys)}]");
            this._logger.LogWarning("Function '{FunctionName}' not found in step '{StepName}'", message.FunctionName, this.Name);
            return;
        }

        Console.WriteLine($"[FunctionStep] {this.Name} Function '{message.FunctionName}' expects parameters: [{string.Join(", ", functionInputs.Keys)}]");

        foreach (var parameter in message.Parameters)
        {
            if (functionInputs.ContainsKey(parameter.Key))
            {
                Console.WriteLine($"[FunctionStep] {this.Name} Setting parameter '{parameter.Key}' = '{parameter.Value}'");
                functionInputs[parameter.Key] = parameter.Value;
            }
            else
            {
                Console.WriteLine($"[FunctionStep] {this.Name} Parameter '{parameter.Key}' not expected by function '{message.FunctionName}'");
            }
        }

        // Also assign the main data if there's a single parameter
        if (message.Data != null && functionInputs.Count == 1)
        {
            var singleParam = functionInputs.Keys.First();
            Console.WriteLine($"[FunctionStep] {this.Name} Assigning message data '{message.Data}' to single parameter '{singleParam}'");
            functionInputs[singleParam] = message.Data;
        }

        Console.WriteLine($"[FunctionStep] {this.Name} After assignment, function '{message.FunctionName}' parameters: [{string.Join(", ", functionInputs.Select(p => $"{p.Key}={p.Value ?? "NULL"}"))}]");
    }

    private List<string> GetExecutableFunctions()
    {
        return this._currentInputs
            .Where(kvp => kvp.Value.All(param => param.Value != null))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private async Task ExecuteFunctionAsync(string functionName)
    {
        Console.WriteLine($"[FunctionStep] {this.Name} ExecuteFunctionAsync called with function: {functionName}");

        if (!this._functions.TryGetValue(functionName, out var function))
        {
            Console.WriteLine($"[FunctionStep] {this.Name} Function '{functionName}' not found. Available functions: {string.Join(", ", this._functions.Keys)}");
            throw new ArgumentException($"Function '{functionName}' not found");
        }

        // Start with the current inputs
        var arguments = new KernelArguments(this._currentInputs[functionName]!);

        // Automatically inject context parameters
        foreach (var parameter in function.Metadata.Parameters)
        {
            if (IsContextParameter(parameter.ParameterType))
            {
                var contextInstance = CreateContextInstance(parameter.ParameterType);
                if (contextInstance != null)
                {
                    Console.WriteLine($"[FunctionStep] {this.Name} Injecting context parameter '{parameter.Name}' of type '{parameter.ParameterType}'");
                    arguments[parameter.Name] = contextInstance;
                }
            }
        }

        Console.WriteLine($"[FunctionStep] {this.Name} About to invoke function '{functionName}' with {arguments.Count} arguments (including injected context)");

        try
        {
            var result = await this._context.Kernel.InvokeAsync(function, arguments).ConfigureAwait(false);
            Console.WriteLine($"[FunctionStep] {this.Name} Function '{functionName}' completed successfully");

            // Emit success event
            var successEvent = ProcessEvent.Create(
                result.GetValue<object>(),
                this._context.ProcessId,
                $"{this.Name}.{functionName}.OnResult",
                KernelProcessEventVisibility.Public);

            await this._context.MessageBus!.EmitEventAsync(successEvent, this._context).ConfigureAwait(false);

            // Reset inputs for this function
            foreach (var key in this._currentInputs[functionName]!.Keys.ToList())
            {
                this._currentInputs[functionName]![key] = this._inputChannels[functionName]![key];
            }

            // Save state
            await this.SaveStepStateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Function '{FunctionName}' execution failed", functionName);
            throw;
        }
    }

    private async Task SaveStepStateAsync()
    {
        if (this._context.StorageManager == null || this._stepState == null)
        {
            return;
        }

        try
        {
            var storageKey = (this._stepInfo.State.StepId, this._stepInfo.State.RunId!);
            var metadata = (this._stepInfo with { State = this._stepState }).ToProcessStateMetadata();

            if (metadata != null)
            {
                await this._context.StorageManager.SaveStepStateDataAsync(storageKey.Item1, storageKey.Item2, metadata).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to save step state for '{StepName}'", this.Name);
        }
    }

    /// <summary>
    /// Gets the current step information with updated state.
    /// </summary>
    /// <returns>The current KernelProcessStepInfo.</returns>
    public async Task<KernelProcessStepInfo> GetStepInfoAsync()
    {
        await this.EnsureInitializedAsync().ConfigureAwait(false);

        // Return the step info with current state
        return this._stepInfo with { State = this._stepState ?? this._stepInfo.State };
    }

    private async Task InitializeEdgeGroupProcessorsAsync()
    {
        if (this._stepInfo.IncomingEdgeGroups == null)
        {
            Console.WriteLine($"[FunctionStep] No incoming edge groups for step {this._stepInfo.State.StepId}");
            return;
        }

        Console.WriteLine($"[FunctionStep] Initializing {this._stepInfo.IncomingEdgeGroups.Count} edge group processors for step {this._stepInfo.State.StepId}");

        foreach (var kvp in this._stepInfo.IncomingEdgeGroups)
        {
            var edgeGroup = kvp.Value;
            var requiredMessages = new HashSet<string>(edgeGroup.MessageSources.Select(ms => $"{ms.SourceStepId}.{ms.MessageType}"));
            var processor = new EdgeGroupProcessor(edgeGroup.GroupId, requiredMessages, edgeGroup.InputMapping);

            Console.WriteLine($"[FunctionStep] Created edge group processor for {edgeGroup.GroupId} with {requiredMessages.Count} required messages: {string.Join(", ", requiredMessages)}");

            // Try to restore from storage
            if (this._context.StorageManager != null)
            {
                try
                {
                    Console.WriteLine($"[FunctionStep] {this.Name} Attempting to restore edge group data for {edgeGroup.GroupId}. Storage key: StepId={this._stepInfo.State.StepId}, Id={this.Id}");
                    var (isGroupEdge, edgeData) = await this._context.StorageManager.GetStepEdgeDataAsync(this._stepInfo.State.StepId, this.Id).ConfigureAwait(false);
                    Console.WriteLine($"[FunctionStep] {this.Name} Storage query result: isGroupEdge={isGroupEdge}, edgeData!=null={edgeData != null}");

                    if (isGroupEdge && edgeData != null && edgeData.TryGetValue(edgeGroup.GroupId, out var groupData) && groupData != null)
                    {
                        Console.WriteLine($"[FunctionStep] {this.Name} Found stored data for group {edgeGroup.GroupId}: {groupData.Count} items");
                        var restoredData = groupData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToObject());
                        processor.RehydrateFromStorage(restoredData);
                        Console.WriteLine($"[FunctionStep] {this.Name} Successfully restored {restoredData.Count} items: [{string.Join(", ", restoredData.Keys)}]");
                    }
                    else
                    {
                        Console.WriteLine($"[FunctionStep] {this.Name} No stored data found for group {edgeGroup.GroupId}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FunctionStep] {this.Name} Exception restoring data: {ex.Message}");
                    this._logger.LogWarning(ex, "Failed to restore edge group data for group {GroupId}", edgeGroup.GroupId);
                }
            }

            this._edgeGroupProcessors[edgeGroup.GroupId] = processor;
        }
    }

    private async Task HandleAllOfMessageAsync(StepMessage message, ProcessContext context)
    {
        Console.WriteLine($"[FunctionStep] HandleAllOfMessageAsync: GroupId={message.GroupId}, EdgeGroupProcessors={this._edgeGroupProcessors.Count}");

        if (!this._edgeGroupProcessors.TryGetValue(message.GroupId!, out var processor))
        {
            this._logger.LogWarning("Received message for unknown edge group {GroupId}. Available groups: {AvailableGroups}", message.GroupId, string.Join(", ", this._edgeGroupProcessors.Keys));
            return;
        }

        this._logger.LogDebug("Processing AllOf message with processor for group {GroupId}", message.GroupId);
        Console.WriteLine($"[FunctionStep] {this.Name} Checking if AllOf is complete...");

        if (processor.TryGetResult(message, out var result))
        {
            Console.WriteLine($"[FunctionStep] {this.Name} *** AllOf COMPLETED! Executing function with {result.Count} parameters ***");

            // All messages received, execute function with mapped parameters
            var functionName = message.FunctionName;
            if (this._functions.TryGetValue(functionName, out var function))
            {
                var arguments = new KernelArguments(result);

                Console.WriteLine($"[FunctionStep] {this.Name} Executing function '{functionName}' with arguments: [{string.Join(", ", arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]");
                var functionResult = await this._context.Kernel.InvokeAsync(function, arguments).ConfigureAwait(false);
                Console.WriteLine($"[FunctionStep] {this.Name} Function '{functionName}' completed successfully, result: {functionResult.GetValue<object>()}");

                // Emit success event
                var successEvent = ProcessEvent.Create(
                    functionResult.GetValue<object>(),
                    this._context.ProcessId,
                    $"{functionName}.OnResult",
                    KernelProcessEventVisibility.Public);

                await this._context.MessageBus!.EmitEventAsync(successEvent, this._context).ConfigureAwait(false);

                // Clear the edge group data
                Console.WriteLine($"[FunctionStep] {this.Name} Clearing AllOf edge data for group {message.GroupId}");
                await this.SaveStepEdgeDataAsync(message.GroupId!, new Dictionary<string, object?>()).ConfigureAwait(false);
            }
        }
        else
        {
            // Not all messages received yet, save partial data
            Console.WriteLine($"[FunctionStep] {this.Name} AllOf not complete yet. Current data count: {processor.MessageData.Count}. Required messages status: {processor.GetStatus().Received}/{processor.GetStatus().Required}");
            Console.WriteLine($"[FunctionStep] {this.Name} Current data keys: [{string.Join(", ", processor.MessageData.Keys)}]");
            await this.SaveStepEdgeDataAsync(message.GroupId!, processor.MessageData).ConfigureAwait(false);
        }

        // Always save step state
        await this.SaveStepStateAsync().ConfigureAwait(false);
    }

    private async Task SaveStepEdgeDataAsync(string groupId, Dictionary<string, object?> messageData)
    {
        Console.WriteLine($"[FunctionStep] SaveStepEdgeDataAsync called: GroupId={groupId}, MessageDataCount={messageData.Count}, StepId={this._stepInfo.State.StepId}, Id={this.Id}");

        if (this._context.StorageManager == null)
        {
            Console.WriteLine($"[FunctionStep] StorageManager is null, cannot save step edge data");
            return;
        }

        try
        {
            var edgeData = new Dictionary<string, Dictionary<string, object?>?>
            {
                { groupId, messageData }
            };

            await this._context.StorageManager.SaveStepEdgeDataAsync(this._stepInfo.State.StepId, this.Id, edgeData, isGroupEdge: true).ConfigureAwait(false);
            Console.WriteLine($"[FunctionStep] SaveStepEdgeDataAsync completed successfully for GroupId={groupId}");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to save step edge data for group {GroupId}", groupId);
        }
    }
}

/// <summary>
/// Message channel adapter that implements IKernelProcessMessageChannel for the new architecture.
/// </summary>
internal sealed class StepMessageChannelAdapter : IKernelProcessMessageChannel
{
    private readonly string _stepName;
    private readonly ProcessContext _processContext;

    public StepMessageChannelAdapter(string stepName, ProcessContext processContext)
    {
        this._stepName = stepName;
        this._processContext = processContext;
    }

    public async ValueTask EmitEventAsync(KernelProcessEvent processEvent)
    {
        Console.WriteLine($"[StepMessageChannelAdapter] {this._stepName} EmitEventAsync called: Id='{processEvent.Id}', Data='{processEvent.Data}', Visibility={processEvent.Visibility}");

        // Create a ProcessEvent and emit through MessageBus
        var processEventInstance = ProcessEvent.Create(
            processEvent.Data,
            this._processContext.ProcessId,
            $"{this._stepName}.{processEvent.Id}",
            processEvent.Visibility);

        await this._processContext.MessageBus!.EmitEventAsync(processEventInstance, this._processContext).ConfigureAwait(false);
    }
}
