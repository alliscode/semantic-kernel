// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel;

/// <summary>
/// A serializable representation of an edge between a source Step and a <see cref="ProcessFunctionTarget"/>.
/// </summary>
public record ProcessEdge
{
    /// <summary>
    /// The unique identifier of the source Step.
    /// </summary>
    public string SourceStepId { get; init; }

    /// <summary>
    /// The collection of <see cref="ProcessFunctionTarget"/>s that are the output of the source Step.
    /// </summary>
    public ProcessFunctionTarget OutputTarget { get; init; }

    /// <summary>
    /// Creates a new instance of the <see cref="ProcessEdge"/> class.
    /// </summary>
    public ProcessEdge(string sourceStepId, ProcessFunctionTarget outputTargets)
    {
        Verify.NotNullOrWhiteSpace(sourceStepId);
        Verify.NotNull(outputTargets);

        this.SourceStepId = sourceStepId;
        this.OutputTarget = outputTargets;
    }
}
