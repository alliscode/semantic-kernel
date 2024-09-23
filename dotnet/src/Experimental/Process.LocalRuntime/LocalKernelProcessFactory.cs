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
    public static Task<LocalKernelProcessContext> StartAsync(KernelProcess process)
    {
        return Task.FromResult(new LocalKernelProcessContext());
    }

    public static Task<LocalKernelProcessContext> StartAsync(string processId)
    {
        return Task.FromResult(new LocalKernelProcessContext());
    }
}
