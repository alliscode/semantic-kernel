// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Process;
public class DaprProcessFactory
{
    private readonly ProcessLocalContext _processLocalContext;

    public DaprProcessFactory(ProcessLocalContext processLocalContext)
    {
        this._processLocalContext = processLocalContext;
    }

    /// <summary>
    /// Starts the specified process.
    /// </summary>
    /// <param name="process">Required: The <see cref="KernelProcess"/> to start running.</param>
    /// <param name="initialEvent">Required: The initial event to start the process.</param>
    /// <param name="processId">Optional: Used to specify the unique Id of the process. If the process already has an Id, it will not be overwritten and this parameter has no effect.</param>
    /// <returns>An instance of <see cref="KernelProcess"/> that can be used to interrogate or stop the running process.</returns>
    public async Task<DaprKernelProcessContext> StartAsync(KernelProcess process, KernelProcessEvent initialEvent, string? processId = null, Func<KernelProcessProxyMessage, Task>? eventHandler = null)
    {
        Verify.NotNull(process);
        Verify.NotNullOrWhiteSpace(process.State?.Name);
        Verify.NotNull(initialEvent);

        this._processLocalContext.RegisterCallback(processId, eventHandler);

        // Assign the process Id if one is provided and the processes does not already have an Id.
        if (!string.IsNullOrWhiteSpace(processId) && string.IsNullOrWhiteSpace(process.State.Id))
        {
            process = process with { State = process.State with { Id = processId } };
        }

        DaprKernelProcessContext processContext = new(process);
        await processContext.StartWithEventAsync(initialEvent).ConfigureAwait(false);
        return processContext;
    }
}
