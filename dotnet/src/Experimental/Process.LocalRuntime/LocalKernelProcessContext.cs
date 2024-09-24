// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Process;

/// <summary>
/// Provides context and actions on a process that is running locally.
/// </summary>
public class LocalKernelProcessContext
{
    private readonly string _processId;
    private readonly LocalProcess _localProcess;
    private Task _processTask;

    internal LocalKernelProcessContext(KernelProcess process)
    {
        Verify.NotNull(process);
        Verify.NotNullOrWhiteSpace(process.State?.Name);

        this._localProcess = new LocalProcess(
            process,
            kernel: new Kernel(),
            parentProcessId: null,
            loggerFactory: null);

        this._processId = this._localProcess.Id;
    }

    internal LocalKernelProcessContext(string processId)
    {
        Verify.NotNull(processId);
        // TODO: Get process by Id
    }

    internal void Start(Kernel kernel, KernelProcessEvent initialEvent)
    {
        this._processTask = this._localProcess.ExecuteAsync(kernel, initialEvent, 100);
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
