// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Process.Workflows;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Builder for converting CPS Topic ObjectModel YAML definition in a process.
/// </summary>
public sealed class ObjectModelBuilder
{
    /// <summary>
    /// %%%
    /// </summary>
    public ProcessActionEnvironment Environment { get; init; } = ProcessActionEnvironment.Default;

    /// <summary>
    /// Builds a process from the provided YAML definition of a CPS Topic ObjectModel.
    /// </summary>
    /// <param name="topicYaml">The YAML string defining the CPS Topic ObjectModel.</param>
    /// <returns>The <see cref="KernelProcess"/> that corresponds with the YAML object model.</returns>
    public KernelProcess Build(string topicYaml)
    {
        ProcessBuilder processBuilder = new("topic");
        ProcessActionWalker walker = new(processBuilder, this.Environment);
        walker.ProcessYaml(topicYaml);
        return processBuilder.Build();
    }
}
