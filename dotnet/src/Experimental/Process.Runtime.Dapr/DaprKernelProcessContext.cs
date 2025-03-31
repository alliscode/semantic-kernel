// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.Interfaces;
using Microsoft.SemanticKernel.Process.Serialization;

namespace Microsoft.SemanticKernel;

/// <summary>
/// A context for a Dapr kernel process.
/// </summary>
public class DaprKernelProcessContext : KernelProcessContext
{
    private readonly IProcess _daprProcess;
    private readonly KernelProcess _process;
    private readonly ActorId _processId;

    internal DaprKernelProcessContext(KernelProcess process)
    {
        Verify.NotNull(process);
        Verify.NotNullOrWhiteSpace(process.State?.Name);

        if (string.IsNullOrWhiteSpace(process.State.Id))
        {
            process = process with { State = process.State with { Id = Guid.NewGuid().ToString() } };
        }

        this._process = process;
        this._processId = new ActorId(process.State.Id);
        this._daprProcess = ActorProxy.Create<IProcess>(this._processId, nameof(ProcessActor));
    }

    /// <summary>
    /// Starts the process with an initial event.
    /// </summary>
    /// <param name="initialEvent">The initial event.</param>
    /// <param name="eventProxyStepId">An optional identifier of an actor requesting to proxy events.</param>
    internal async Task StartWithEventAsync(KernelProcessEvent initialEvent, ActorId? eventProxyStepId = null)
    {
        IEventPoll eventStream = ActorProxy.Create<IEventPoll>(this._processId, nameof(ProcessActor));
        CancellationTokenSource tcs = new();

        try
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    if (tcs.IsCancellationRequested)
                    {
                        return;
                    }

                    // Check if the process is still running.
                    var events = await eventStream.GetAvailableAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            var daprProcess = DaprProcessInfo.FromKernelProcess(this._process);
            await this._daprProcess.InitializeProcessAsync(daprProcess, null, eventProxyStepId?.GetId()).ConfigureAwait(false);
            await this._daprProcess.RunOnceAsync(initialEvent.ToJson()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            int x = 3;
        }
        finally
        {
            // Cancel the event stream task.
            tcs.Cancel();
        }
    }

    /// <summary>
    /// Sends a message to the process.
    /// </summary>
    /// <param name="processEvent">The event to sent to the process.</param>
    /// <returns>A <see cref="Task"/></returns>
    public override async Task SendEventAsync(KernelProcessEvent processEvent) =>
        await this._daprProcess.SendMessageAsync(processEvent.ToJson()).ConfigureAwait(false);

    /// <summary>
    /// Stops the process.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    public override async Task StopAsync() => await this._daprProcess.StopAsync().ConfigureAwait(false);

    /// <summary>
    /// Gets a snapshot of the current state of the process.
    /// </summary>
    /// <returns>A <see cref="Task{T}"/> where T is <see cref="KernelProcess"/></returns>
    public override async Task<KernelProcess> GetStateAsync()
    {
        var daprProcessInfo = await this._daprProcess.GetProcessInfoAsync().ConfigureAwait(false);
        return daprProcessInfo.ToKernelProcess();
    }

    /// <inheritdoc/>
    public override Task<IExternalKernelProcessMessageChannel?> GetExternalMessageChannelAsync()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override async Task<string> GetProcessIdAsync()
    {
        var processInfo = await this._daprProcess.GetProcessInfoAsync().ConfigureAwait(false);
        return processInfo.State.Id!;
    }
}
