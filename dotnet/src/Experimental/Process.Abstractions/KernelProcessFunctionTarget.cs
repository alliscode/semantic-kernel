// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;


/// <summary>
/// A serializable representation of a specific parameter of a specific function of a specific Step.
/// </summary>
public record KernelProcessTarget
{
    /// <summary>
    /// Creates an instance of the <see cref="KernelProcessFunctionTarget"/> class.
    /// </summary>
    public KernelProcessTarget(string stepId)
    {
        Verify.NotNullOrWhiteSpace(stepId);
        this.StepId = stepId;
    }

    /// <summary>
    /// The unique identifier of the Step being targeted.
    /// </summary>
    public string StepId { get; init; }
}

/// <summary>
/// A serializable representation of a specific parameter of a specific function of a specific Step.
/// </summary>
public record KernelProcessEventTarget : KernelProcessTarget
{
    /// <summary>
    /// Creates an instance of the <see cref="KernelProcessFunctionTarget"/> class.
    /// </summary>
    public KernelProcessEventTarget(string stepId, string eventName)
        : base(stepId)
    {
        Verify.NotNullOrWhiteSpace(eventName);

        this.EventName = eventName;
    }

    /// <summary>
    /// The name of the Event to target.
    /// </summary>
    public string EventName { get; init; }
}

/// <summary>
/// A serializable representation of a specific parameter of a specific function of a specific Step.
/// </summary>
public record KernelProcessFunctionTarget : KernelProcessTarget
{
    /// <summary>
    /// Creates an instance of the <see cref="KernelProcessFunctionTarget"/> class.
    /// </summary>
    public KernelProcessFunctionTarget(string stepId, string functionName, string? parameterName = null)
        : base(stepId)
    {
        Verify.NotNullOrWhiteSpace(functionName);

        this.FunctionName = functionName;
        this.ParameterName = parameterName;
    }

    /// <summary>
    /// The name of the Kernel Function to target.
    /// </summary>
    public string FunctionName { get; init; }

    /// <summary>
    /// The name of the parameter to target. This may be null if the function has no parameters.
    /// </summary>
    public string? ParameterName { get; init; }
}
