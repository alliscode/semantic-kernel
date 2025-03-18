// Copyright (c) Microsoft. All rights reserved.
using System.Linq;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.Models;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality to allow emitting external messages from within the SK
/// process.
/// </summary>
public sealed class ProcessProxyBuilder : ProcessStepBuilder<KernelProxyStep>
{
    internal ProcessProxyBuilder(string name, ProcessBuilder? processBuilder = null)
        : base(name, processBuilder)
    {
    }

    /// <summary>
    /// Version of the proxy step, used when saving the state of the step.
    /// </summary>
    public string Version { get; init; } = "v1";

    internal ProcessFunctionTargetBuilder GetExternalFunctionTargetBuilder()
    {
        return new ProcessFunctionTargetBuilder(this, functionName: KernelProxyStep.Functions.EmitExternalEvent, parameterName: "proxyEvent");
    }

    /// <inheritdoc/>
    internal override KernelProcessStepInfo BuildStep(KernelProcessStepStateMetadata? stateMetadata = null)
    {
        var builtEdges = this.Edges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(e => e.Build()).ToList());
        KernelProcessStepState state = new(this.Name, this.Version, this.Id);
        return new KernelProcessProxy(state, builtEdges);
    }
}
