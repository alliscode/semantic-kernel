// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.SemanticKernel.KernelProcess;

namespace Microsoft.SemanticKernel.Process;

/// <summary>
/// Process step builder for a delegate step.
/// </summary>
public class ProcessDelegateBuilder : ProcessStepBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessDelegateBuilder"/> class.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="stepFunction"></param>
    /// <param name="processBuilder"></param>
    public ProcessDelegateBuilder(string id, StepFunction stepFunction, ProcessBuilder? processBuilder) : base(id, processBuilder)
    {
        this.StepFunction = stepFunction ?? throw new ArgumentNullException(nameof(stepFunction), "Step function cannot be null.");
    }

    /// <summary>
    /// Version of the map-step, used when saving the state of the step.
    /// </summary>
    public string Version { get; init; } = "v1";

    /// <summary>
    /// Ste funtion
    /// </summary>
    public StepFunction StepFunction { get; init; } = null!;

    internal override KernelProcessStepInfo BuildStep(ProcessBuilder processBuilder)
    {
        // Build the edges first
        var builtEdges = this.Edges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(e => e.Build()).ToList());

        KernelProcessMapState state = new(this.StepId, this.Version, this.StepId);

        return new KernelProcessDelegateStepInfo(typeof(KernelDelegateProcessStep), state, builtEdges)
        {
            StepFunction = this.StepFunction
        };
    }

    internal override Dictionary<string, KernelFunctionMetadata> GetFunctionMetadataMap()
    {
        throw new NotImplementedException();
    }
}
