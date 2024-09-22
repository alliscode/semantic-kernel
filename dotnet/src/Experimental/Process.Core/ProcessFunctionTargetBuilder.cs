// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process function target.
/// </summary>
public sealed class ProcessFunctionTargetBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessFunctionTargetBuilder"/> class.
    /// </summary>
    /// <param name="step">The step to target.</param>
    /// <param name="functionName">The function to target.</param>
    /// <param name="parameterName">The parameter to target.</param>
    public ProcessFunctionTargetBuilder(ProcessStepBuilder step, string? functionName = null, string? parameterName = null)
    {
        Verify.NotNull(step);
        this.Step = step;

        var target = step.ResolveFunctionTarget(functionName, parameterName);
        Verify.NotNull(target);

        this.FunctionName = target.FunctionName!;
        this.ParameterName = target.ParameterName;
    }

    /// <summary>
    /// An instance of <see cref="ProcessStepBuilder"/> representing the target Step.
    /// </summary>
    public ProcessStepBuilder Step { get; init; }

    /// <summary>
    /// The name of the function to target.
    /// </summary>
    public string FunctionName { get; init; }

    /// <summary>
    /// The name of the parameter to target. This may be null if the function has no parameters.
    /// </summary>
    public string? ParameterName { get; init; }
}
