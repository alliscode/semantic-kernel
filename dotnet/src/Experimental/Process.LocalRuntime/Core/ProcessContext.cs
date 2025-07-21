// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Process.Runtime;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


internal delegate bool ProcessEventProxy(ProcessEvent processEvent);

/// <summary>
/// Context information for process execution, maintaining hierarchy and shared resources.
/// </summary>
internal sealed class ProcessContext
{
    /// <summary>
    /// The unique identifier of the current process.
    /// </summary>
    public string ProcessId { get; init; } = string.Empty;

    /// <summary>
    /// The identifier of the parent process, if this is a sub-process.
    /// </summary>
    public string? ParentProcessId { get; init; }

    /// <summary>
    /// The identifier of the root process in the hierarchy.
    /// </summary>
    public string RootProcessId { get; init; } = string.Empty;

    /// <summary>
    /// The kernel instance for step execution.
    /// </summary>
    public Kernel Kernel { get; init; } = null!;

    /// <summary>
    /// Event proxy for intercepting and filtering events.
    /// </summary>
    public ProcessEventProxy? EventProxy { get; init; }

    /// <summary>
    /// External message channel for communication outside the process.
    /// </summary>
    public IExternalKernelProcessMessageChannel? ExternalMessageChannel { get; init; }

    /// <summary>
    /// Storage manager for persisting process state.
    /// </summary>
    public ProcessStorageManager? StorageManager { get; init; }

    /// <summary>
    /// Message bus for routing events and messages.
    /// </summary>
    public IMessageBus? MessageBus { get; set; }

    /// <summary>
    /// Creates a child context for sub-processes.
    /// </summary>
    /// <param name="childProcessId">The ID of the child process.</param>
    /// <returns>A new ProcessContext for the child.</returns>
    public ProcessContext CreateChildContext(string childProcessId)
    {
        return new ProcessContext
        {
            ProcessId = childProcessId,
            ParentProcessId = this.ProcessId,
            RootProcessId = this.RootProcessId,
            Kernel = this.Kernel,
            EventProxy = this.EventProxy,
            ExternalMessageChannel = this.ExternalMessageChannel,
            StorageManager = this.StorageManager,
            MessageBus = this.MessageBus
        };
    }
}