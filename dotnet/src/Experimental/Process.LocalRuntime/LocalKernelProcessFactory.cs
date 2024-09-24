// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.Process;

namespace Microsoft.SemanticKernel;

/// <summary>
/// A class that can run a process locally or in-process.
/// </summary>
public static class LocalKernelProcessFactory
{
    /// <summary>
    /// Starts the specified process.
    /// </summary>
    /// <param name="process">The <see cref="KernelProcess"/> to start running.</param>
    /// <returns>An instance of <see cref="KernelProcess"/> that can be used to interogate or stop the running process.</returns>
    public static Task<LocalKernelProcessContext> StartAsync(this KernelProcess process, Kernel kernel, KernelProcessEvent initialEvent)
    {
        var processContext = new LocalKernelProcessContext(process);
        processContext.Start(kernel, initialEvent);
        return Task.FromResult(processContext);
    }

    /// <summary>
    /// Starts the existing process with the specified Id.
    /// </summary>
    /// <param name="processId">The unique Id of the process.</param>
    /// <returns>An instance of <see cref="KernelProcess"/> that can be used to interogate or stop the running process.</returns>
    public static Task<LocalKernelProcessContext> StartAsync(string processId)
    {
        return Task.FromResult(new LocalKernelProcessContext(processId));
    }
}
