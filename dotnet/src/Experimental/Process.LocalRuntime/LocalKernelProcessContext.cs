// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Process;

/// <summary>
/// Provides context and actions on a process that is running locally.
/// </summary>
public class LocalKernelProcessContext
{
    private readonly string _processId;

    internal LocalKernelProcessContext(KernelProcess process)
    {
        Verify.NotNull(process);
        _processId = process.State?.Id ?? Guid.NewGuid().ToString();

        // TODO: build internal representation of the process
    }

    internal LocalKernelProcessContext(string processId)
    {
        Verify.NotNull(processId);
        // TODO: Get process by Id
    }

    internal Task StartAsync()
    {
        return Task.CompletedTask;
    }


    /// <summary>
    /// Stops the running process.
    /// </summary>
    public ValueTask StopAsync()
    {
        return default;
    }

    /// <summary>
    /// Sends an event to the running process.
    /// </summary>
    /// <param name="eventId">The unique Id of the event.</param>
    /// <param name="eventData">An optional data object to send with the event.</param>
    /// <returns></returns>
    public ValueTask SendEventAsync(string eventId, object? eventData)
    {
        return default;
    }
}
