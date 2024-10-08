// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Dapr.Actors;

namespace Microsoft.SemanticKernel;

/// <summary>
/// An interface that represents a step in a process.
/// </summary>
internal interface IStep : IActor
{
    ValueTask InitializeStepAsync(KernelProcessStepInfo stepInfo, string? parentProcessId);

    Task<int> PrepareIncomingMessagesAsync();

    Task ProcessIncomingMessagesAsync();

    Task<KernelProcessStepInfo> ToKernelProcessStepInfoAsync();
}
