// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.LocalRuntime.Core;
using Core = Microsoft.SemanticKernel.Process.LocalRuntime.Core;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides context and actions on a process that is running locally.
/// </summary>
public sealed class LocalKernelProcessContext : KernelProcessContext, System.IAsyncDisposable
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly ProcessContext _processContext;
    private readonly Kernel _kernel;

    internal LocalKernelProcessContext(KernelProcess process, Kernel kernel, ProcessEventProxy? eventProxy = null, IExternalKernelProcessMessageChannel? externalMessageChannel = null, IProcessStorageConnector? storageConnector = null, string? instanceId = null)
    {
        Console.WriteLine("[LocalKernelProcessContext] Constructor called - NEW ARCHITECTURE IS BEING USED!");
        Verify.NotNull(process, nameof(process));
        Verify.NotNull(kernel, nameof(kernel));
        Verify.NotNullOrWhiteSpace(process.State?.StepId);

        this._kernel = kernel;

        // Create process context
        this._processContext = new ProcessContext
        {
            ProcessId = instanceId ?? System.Guid.NewGuid().ToString(),
            ParentProcessId = null, // This is a root process
            RootProcessId = instanceId ?? System.Guid.NewGuid().ToString(),
            Kernel = kernel,
            EventProxy = eventProxy != null ? (Core.ProcessEventProxy)((processEvent) => eventProxy(processEvent)) : null,
            ExternalMessageChannel = externalMessageChannel,
            StorageManager = storageConnector != null ? new ProcessStorageManager(storageConnector) : null
        };

        // Create orchestrator
        this._orchestrator = new ProcessOrchestrator(process, this._processContext);
    }

    internal Task StartWithEventAsync(KernelProcessEvent initialEvent, Kernel? kernel = null) =>
        this._orchestrator.ExecuteOnceAsync(initialEvent);

    /// <summary>
    /// Sends a message to the process.
    /// </summary>
    /// <param name="processEvent">The event to sent to the process.</param>
    /// <returns>A <see cref="Task"/></returns>
    public override Task SendEventAsync(KernelProcessEvent processEvent) =>
        this._orchestrator.SendMessageAsync(processEvent);

    /// <summary>
    /// Stops the process.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    public override Task StopAsync() => Task.CompletedTask; // Simplified: no long-running processes to stop

    /// <summary>
    /// Gets a snapshot of the current state of the process.
    /// </summary>
    /// <returns>A <see cref="Task{T}"/> where T is <see cref="KernelProcess"/></returns>
    public override Task<KernelProcess> GetStateAsync() => this._orchestrator.GetProcessInfoAsync();

    /// <summary>
    /// Disposes of the resources used by the process.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await this._orchestrator.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task<IExternalKernelProcessMessageChannel?> GetExternalMessageChannelAsync()
    {
        return Task.FromResult(this._processContext.ExternalMessageChannel);
    }

    /// <inheritdoc/>
    public override Task<string> GetProcessIdAsync() => Task.FromResult(this._processContext.ProcessId);

    /// <summary>
    /// Read the step states in from the process.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public override Task<IDictionary<string, KernelProcessStepState>> GetStepStatesAsync()
    {
        throw new System.NotImplementedException();
    }
}
