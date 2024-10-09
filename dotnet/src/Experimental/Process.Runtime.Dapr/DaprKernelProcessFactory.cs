// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.SemanticKernel;
/// <summary>
/// A class that can run a process locally or in-process.
/// </summary>
public static class DaprKernelProcessFactory
{
    /// <summary>
    /// Starts the specified process.
    /// </summary>
    /// <param name="process">Required: The <see cref="KernelProcess"/> to start running.</param>
    /// <param name="kernel">Required: An instance of <see cref="Kernel"/></param>
    /// <param name="initialEvent">Required: The initial event to start the process.</param>
    /// <returns>An instance of <see cref="KernelProcess"/> that can be used to interrogate or stop the running process.</returns>
    public static async Task<DaprKernelProcessContext> StartAsync(this KernelProcess process, Kernel kernel, KernelProcessEvent initialEvent)
    {
        Verify.NotNull(process);
        Verify.NotNullOrWhiteSpace(process.State?.Name);
        Verify.NotNull(kernel);
        Verify.NotNull(initialEvent);

        var processContext = new DaprKernelProcessContext(process, kernel);
        await processContext.StartWithEventAsync(initialEvent).ConfigureAwait(false);
        return processContext;
    }
}
