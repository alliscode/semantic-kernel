// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Process.Runtime;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


/// <summary>
/// Interface for centralized message routing and event handling.
/// </summary>
internal interface IMessageBus
{
    /// <summary>
    /// Enqueues a message for processing in the next superstep.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    void EnqueueMessage(StepMessage message);

    /// <summary>
    /// Emits an event that may trigger additional messages.
    /// </summary>
    /// <param name="processEvent">The event to emit.</param>
    /// <param name="context">The process context.</param>
    /// <returns>A task representing the event emission.</returns>
    Task EmitEventAsync(ProcessEvent processEvent, ProcessContext context);

    /// <summary>
    /// Gets all pending messages for the next superstep and clears the queue.
    /// </summary>
    /// <returns>The pending messages.</returns>
    IReadOnlyList<StepMessage> GetPendingMessages();

    /// <summary>
    /// Adds an external event to the message queue.
    /// </summary>
    /// <param name="externalEvent">The external event to process.</param>
    /// <param name="context">The process context.</param>
    /// <returns>A task representing the event processing.</returns>
    Task AddExternalEventAsync(KernelProcessEvent externalEvent, ProcessContext context);

    /// <summary>
    /// Registers an edge group with the message bus for AllOf processing.
    /// </summary>
    /// <param name="edgeGroup">The edge group to register.</param>
    void RegisterEdgeGroup(KernelProcessEdgeGroup edgeGroup);
}