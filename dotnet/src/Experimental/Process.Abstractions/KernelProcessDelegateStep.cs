// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using static Microsoft.SemanticKernel.KernelProcess;

namespace Microsoft.SemanticKernel.Process;

/// <summary>
/// Delegate step in a Kernel Process.
/// </summary>
public record KernelProcessDelegateStep : KernelProcessStepInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelProcessDelegateStep"/> class.
    /// </summary>
    /// <param name="innerStepType"></param>
    /// <param name="state"></param>
    /// <param name="edges"></param>
    /// <param name="incomingEdgeGroups"></param>
    public KernelProcessDelegateStep(Type innerStepType, KernelProcessStepState state, Dictionary<string, List<KernelProcessEdge>> edges, Dictionary<string, KernelProcessEdgeGroup>? incomingEdgeGroups = null) : base(innerStepType, state, edges, incomingEdgeGroups)
    {
    }

    /// <summary>
    /// Ste funtion
    /// </summary>
    public StepFunction StepFunction { get; init; } = null!;
}
