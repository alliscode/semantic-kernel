// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Dapr.Actors;

namespace Microsoft.SemanticKernel;

/// <summary>
/// An interface that represents a process.
/// </summary>
internal interface IProcess : IActor
{
    ValueTask InitializeProcessAsync(KernelProcess process, string? parentProcessId);

    Task StartAsync(Kernel? kernel = null, bool keepAlive = true);

    Task RunOnceAsync(KernelProcessEvent? processEvent, Kernel? kernel = null);

    Task StopAsync();

    Task SendMessageAsync(KernelProcessEvent processEvent, Kernel? kernel = null);

    Task<KernelProcess> GetProcessInfoAsync();
}
