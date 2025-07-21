// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Runtime;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


/// <summary>
/// Centralized message bus for routing events and managing message queues.
/// </summary>
internal sealed class MessageBus : IMessageBus
{
    private readonly Queue<StepMessage> _messageQueue = new();
    private readonly Dictionary<string, List<KernelProcessEdge>> _edges;
    private readonly Dictionary<string, EdgeGroupProcessor> _edgeGroupProcessors = new();
    private readonly Dictionary<string, KernelProcessEdgeGroup> _edgeGroups = new();

    public MessageBus(IReadOnlyDictionary<string, IReadOnlyCollection<KernelProcessEdge>>? processEdges, IList<KernelProcessStepInfo>? steps = null, string? processId = null)
    {
        // Start with process-level edges (input/output events)
        this._edges = processEdges?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList()) ?? new Dictionary<string, List<KernelProcessEdge>>();

        // Discover and add step-to-step edges
        if (steps != null)
        {
            this.DiscoverStepEdges(steps, processId);
        }
    }

    /// <summary>
    /// Discovers all step-to-step edges and adds them to the internal routing table.
    /// </summary>
    private void DiscoverStepEdges(IList<KernelProcessStepInfo> steps, string? processId)
    {
        Console.WriteLine($"[MessageBus] Discovering step edges from {steps.Count} steps for process '{processId}'");
        int totalEdgesDiscovered = 0;

        foreach (var step in steps)
        {
            if (step.Edges != null)
            {
                foreach (var stepEdgeKvp in step.Edges)
                {
                    var originalEdgeKey = stepEdgeKvp.Key;
                    var stepEdges = stepEdgeKvp.Value;

                    // Create qualified edge key with processId prefix
                    var qualifiedEdgeKey = !string.IsNullOrEmpty(processId)
                        ? $"{processId}.{originalEdgeKey}"
                        : originalEdgeKey;

                    Console.WriteLine($"[MessageBus] Found {stepEdges.Count} edges for event '{originalEdgeKey}' from step '{step.State.StepId}', registering as '{qualifiedEdgeKey}'");

                    if (this._edges.TryGetValue(qualifiedEdgeKey, out List<KernelProcessEdge>? value))
                    {
                        value.AddRange(stepEdges);
                    }
                    else
                    {
                        // Otherwise, create a new entry
                        this._edges[qualifiedEdgeKey] = stepEdges.ToList();
                    }

                    totalEdgesDiscovered += stepEdges.Count;
                }
            }

            // Recursively discover edges from nested processes
            if (step is KernelProcess nestedProcess && nestedProcess.Steps != null)
            {
                this.DiscoverStepEdges(nestedProcess.Steps, processId);
            }
        }

        Console.WriteLine($"[MessageBus] Discovered {totalEdgesDiscovered} step-to-step edges. Total edges in routing table: {this._edges.Sum(kvp => kvp.Value.Count)}");
        Console.WriteLine($"[MessageBus] All edge keys in routing table: [{string.Join(", ", this._edges.Keys)}]");
    }

    /// <summary>
    /// Registers an edge group with the message bus.
    /// </summary>
    /// <param name="edgeGroup">The edge group to register.</param>
    public void RegisterEdgeGroup(KernelProcessEdgeGroup edgeGroup)
    {
        this._edgeGroups[edgeGroup.GroupId] = edgeGroup;
    }

    public void EnqueueMessage(StepMessage message)
    {
        this._messageQueue.Enqueue(message);
    }

    public async Task EmitEventAsync(ProcessEvent processEvent, ProcessContext context)
    {
        Console.WriteLine($"[MessageBus] EmitEventAsync called: QualifiedId='{processEvent.QualifiedId}', SourceId='{processEvent.SourceId}'");

        // Apply event proxy filtering
        if (context.EventProxy?.Invoke(processEvent) == false)
        {
            return; // Event was filtered out
        }

        // Get edges for this event
        var eventId = processEvent.QualifiedId;
        Console.WriteLine($"[MessageBus] Looking for edges with key: '{eventId}'. Available keys: [{string.Join(", ", this._edges.Keys)}]");

        if (!this._edges.TryGetValue(eventId, out var edges) || edges == null || edges.Count == 0)
        {
            // Check for global error event if this is an error
            if (processEvent.IsError && this._edges.TryGetValue(ProcessConstants.GlobalErrorEventId, out var errorEdges))
            {
                edges = errorEdges;
            }
            else
            {
                return; // No edges to process
            }
        }

        // Process all edges for this event
        await this.ProcessEdgesAsync(edges, processEvent, context).ConfigureAwait(false);
    }

    public IReadOnlyList<StepMessage> GetPendingMessages()
    {
        var messages = this._messageQueue.ToArray();
        this._messageQueue.Clear();
        return messages;
    }

    public async Task AddExternalEventAsync(KernelProcessEvent externalEvent, ProcessContext context)
    {
        if (!this._edges.TryGetValue(externalEvent.Id, out var edges) || edges == null)
        {
            return; // No edges to process for this external event
        }

        var processEvent = ProcessEvent.Create(
            externalEvent.Data,
            context.ProcessId,
            externalEvent.Id,
            externalEvent.Visibility);

        await this.ProcessEdgesAsync(edges, processEvent, context).ConfigureAwait(false);
    }

    private async Task ProcessEdgesAsync(
        List<KernelProcessEdge> edges,
        ProcessEvent processEvent,
        ProcessContext context)
    {
        bool foundMatchingEdge = false;
        var defaultEdges = new List<KernelProcessEdge>();

        foreach (var edge in edges)
        {
            // Handle default conditions separately
            if (edge.Condition.IsDefault())
            {
                defaultEdges.Add(edge);
                continue;
            }

            // Evaluate edge condition
            bool isConditionMet = await edge.Condition.Callback(
                processEvent.ToKernelProcessEvent(),
                null).ConfigureAwait(false); // TODO: Pass process state if needed

            if (!isConditionMet)
            {
                continue;
            }

            await this.ProcessSingleEdgeAsync(edge, processEvent, context).ConfigureAwait(false);
            foundMatchingEdge = true;
        }

        // Process default edges if no other edges matched
        if (!foundMatchingEdge && defaultEdges.Count > 0)
        {
            foreach (var edge in defaultEdges)
            {
                await this.ProcessSingleEdgeAsync(edge, processEvent, context).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessSingleEdgeAsync(
        KernelProcessEdge edge,
        ProcessEvent processEvent,
        ProcessContext context)
    {
        Console.WriteLine($"[MessageBus] ProcessSingleEdgeAsync: Event={processEvent.QualifiedId}, EdgeGroupId={edge.GroupId ?? "NULL"}, Target={edge.OutputTarget.GetType().Name}");

        switch (edge.OutputTarget)
        {
            case KernelProcessFunctionTarget functionTarget:
                var message = this.CreateStepMessage(edge, processEvent, functionTarget);
                this.EnqueueMessage(message);
                break;

            case KernelProcessAgentInvokeTarget agentTarget:
                var agentMessage = this.CreateAgentStepMessage(edge, processEvent, agentTarget);
                this.EnqueueMessage(agentMessage);
                break;

            case KernelProcessStateTarget stateTarget:
                // TODO: Implement state updates
                await Task.CompletedTask.ConfigureAwait(false);
                break;

            case KernelProcessEmitTarget emitTarget:
                // TODO: Implement emit targets
                await Task.CompletedTask.ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unsupported edge target type: {edge.OutputTarget.GetType()}");
        }
    }

    private StepMessage CreateStepMessage(
        KernelProcessEdge edge,
        ProcessEvent processEvent,
        KernelProcessFunctionTarget target)
    {
        var parameters = new Dictionary<string, object?>();

        // Map edge parameters to function parameters
        if (target.ParameterName != null && processEvent.Data != null)
        {
            parameters[target.ParameterName] = processEvent.Data;
        }

        var message = new StepMessage
        {
            SourceId = processEvent.SourceId,
            DestinationId = target.StepId,
            FunctionName = target.FunctionName,
            SourceEventId = processEvent.QualifiedId, // Use qualified ID for proper edge group tracking
            TargetEventId = null,
            Data = processEvent.Data,
            Parameters = parameters,
            GroupId = edge.GroupId, // Set the group ID from the edge
            ThreadId = processEvent.WrittenToThread
        };

        if (!string.IsNullOrEmpty(edge.GroupId))
        {
            Console.WriteLine($"[MessageBus] Created AllOf message: GroupId={edge.GroupId}, Source={processEvent.SourceId}, Event={processEvent.QualifiedId}, Destination={target.StepId}");
        }
        else
        {
            Console.WriteLine($"[MessageBus] Created regular message: Source={processEvent.SourceId}, Event={processEvent.QualifiedId}, Destination={target.StepId}");
        }

        return message;
    }

    private StepMessage CreateAgentStepMessage(
        KernelProcessEdge edge,
        ProcessEvent processEvent,
        KernelProcessAgentInvokeTarget target)
    {
        return new StepMessage
        {
            SourceId = processEvent.SourceId,
            DestinationId = target.StepId,
            FunctionName = "Invoke", // Agent steps use a standard Invoke function
            SourceEventId = processEvent.SourceId,
            TargetEventId = null,
            Data = processEvent.Data,
            Parameters = new Dictionary<string, object?>(),
            GroupId = null,
            ThreadId = processEvent.WrittenToThread
        };
    }

    /// <summary>
    /// Processes a message that belongs to an edge group (AllOf functionality).
    /// </summary>
    private async Task ProcessEdgeGroupMessageAsync(StepMessage message, ProcessContext context)
    {
        if (string.IsNullOrEmpty(message.GroupId))
        {
            return;
        }

        // Get or create the edge group processor
        if (!this._edgeGroupProcessors.TryGetValue(message.GroupId, out var processor))
        {
            // Load any existing edge data from storage
            var existingData = await this.LoadEdgeGroupDataAsync(message.GroupId, message.DestinationId, message.DestinationId, context).ConfigureAwait(false);

            // Get the edge group definition with input mapping
            this._edgeGroups.TryGetValue(message.GroupId, out var edgeGroup);
            var inputMapping = edgeGroup?.InputMapping;

            // Create processor with required message keys and input mapping
            var requiredMessages = this.GetRequiredMessagesForGroup(message.GroupId);
            processor = new EdgeGroupProcessor(message.GroupId, requiredMessages, inputMapping);

            // Restore any existing data
            if (existingData != null && existingData.Count > 0)
            {
                processor.RehydrateFromStorage(existingData);
            }

            this._edgeGroupProcessors[message.GroupId] = processor;
        }

        // Try to get result from the processor
        if (processor.TryGetResult(message, out var result))
        {
            // All required messages have arrived - create the final message
            var finalMessage = new StepMessage
            {
                SourceId = message.SourceId,
                DestinationId = message.DestinationId,
                FunctionName = message.FunctionName,
                SourceEventId = message.SourceEventId,
                TargetEventId = message.TargetEventId,
                Data = null, // Data will come from parameters
                Parameters = result,
                GroupId = null, // Clear group ID since processing is complete
                ThreadId = message.ThreadId
            };

            this.EnqueueMessage(finalMessage);

            // Remove the processor and clean up storage
            this._edgeGroupProcessors.Remove(message.GroupId);
            await this.ClearEdgeGroupDataAsync(message.GroupId, message.DestinationId, message.DestinationId, context).ConfigureAwait(false);
        }
        else
        {
            // Save the partial data to storage
            await this.SaveEdgeGroupDataAsync(message.GroupId, message.DestinationId, message.DestinationId, processor.MessageData, context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the required message keys for an edge group.
    /// </summary>
    private HashSet<string> GetRequiredMessagesForGroup(string groupId)
    {
        // Collect all edge event IDs that belong to this group
        var requiredMessages = new HashSet<string>();

        foreach (var kvp in this._edges)
        {
            string eventId = kvp.Key;
            foreach (var edge in kvp.Value.Where(e => e.GroupId == groupId))
            {
                // Strip process ID prefix to match SourceId format used by EdgeGroupProcessor
                string messageKey = eventId;
                int dotIndex = eventId.IndexOf('.');
                if (dotIndex > 0)
                {
                    // Remove "processId." prefix to get "StepName.EventName" format
                    messageKey = eventId.Substring(dotIndex + 1);
                }

                requiredMessages.Add(messageKey);
            }
        }

        return requiredMessages;
    }

    /// <summary>
    /// Loads edge group data from storage.
    /// </summary>
    private async Task<Dictionary<string, object?>> LoadEdgeGroupDataAsync(string groupId, string stepName, string stepId, ProcessContext context)
    {
        if (context.StorageManager == null)
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            var (isGroupEdge, edgesData) = await context.StorageManager.GetStepEdgeDataAsync(stepName, stepId).ConfigureAwait(false);

            if (isGroupEdge && edgesData != null && edgesData.TryGetValue(groupId, out var edgeData) && edgeData != null)
            {
                // Convert from KernelProcessEventData to object
                var result = new Dictionary<string, object?>();
                foreach (var kvp in edgeData)
                {
                    result[kvp.Key] = kvp.Value?.ToObject();
                }
                return result;
            }
        }
        catch (Exception ex)
        {
            // Log error but continue - we'll start with empty data
            System.Diagnostics.Debug.WriteLine($"Failed to load edge group data: {ex.Message}");
        }

        return new Dictionary<string, object?>();
    }

    /// <summary>
    /// Saves edge group data to storage.
    /// </summary>
    private async Task SaveEdgeGroupDataAsync(string groupId, string stepName, string stepId, Dictionary<string, object?> data, ProcessContext context)
    {
        if (context.StorageManager == null)
        {
            return;
        }

        try
        {
            var stepEdgesData = new Dictionary<string, Dictionary<string, object?>?>
            {
                { groupId, data }
            };

            await context.StorageManager.SaveStepEdgeDataAsync(stepName, stepId, stepEdgesData, isGroupEdge: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log error but continue
            System.Diagnostics.Debug.WriteLine($"Failed to save edge group data: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears edge group data from storage.
    /// </summary>
    private async Task ClearEdgeGroupDataAsync(string groupId, string stepName, string stepId, ProcessContext context)
    {
        if (context.StorageManager == null)
        {
            return;
        }

        try
        {
            var emptyEdgesData = new Dictionary<string, Dictionary<string, object?>?>
            {
                { groupId, new Dictionary<string, object?>() }
            };

            await context.StorageManager.SaveStepEdgeDataAsync(stepName, stepId, emptyEdgesData, isGroupEdge: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log error but continue
            System.Diagnostics.Debug.WriteLine($"Failed to clear edge group data: {ex.Message}");
        }
    }
}
