// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Process;

/// <summary>
/// A serializable representation of a Process.
/// </summary>
public class Process : ProcessStep<ProcessState>
{
    /// <summary>
    /// The collection of Steps in the Process.
    /// </summary>
    public IList<ProcessStep> Steps { get; init; }

    /// <summary>
    /// The unique identifier of the Step that is defined as the entry point to the process.
    /// </summary>
    public string EntryPointId { get; init; }

    /// <summary>
    /// Creates a new instance of the <see cref="Process"/> class.
    /// </summary>
    /// <param name="name">The human friendly name of the Process.</param>
    public Process(string name)
    {
        Verify.NotNullOrWhiteSpace(name, nameof(name));
        Verify.NotNullOrWhiteSpace(this.EntryPointId, nameof(this.EntryPointId));

        this.Steps = new List<ProcessStep>();
        this.State.State = new ProcessState
        {
            Name = name
        };
    }
}
