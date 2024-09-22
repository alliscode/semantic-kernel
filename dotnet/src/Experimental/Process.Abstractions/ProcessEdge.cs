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
    public IList<ProcessFunctionTarget> OutputTargets { get; init; }

    /// <summary>
    /// Creates a new instance of the <see cref="ProcessEdge"/> class.
    /// </summary>
    public ProcessEdge()
    {
        Verify.NotNullOrWhiteSpace(this.SourceStepId);
        Verify.NotNull(this.OutputTargets);
    }
}
