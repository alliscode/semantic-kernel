// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process function target.
/// </summary>
public abstract class ProcessTargetBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessFunctionTargetBuilder"/> class.
    /// </summary>
    /// <param name="step">The step to target.</param>
    protected ProcessTargetBuilder(ProcessStepBuilder step)
    {
        Verify.NotNull(step);
        this.Step = step;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessFunctionTargetBuilder"/> class with an instance of <see cref="ProcessBuilder"/>
    /// as the step. This is used when the target is another process.
    /// </summary>
    /// <param name="step"></param>
    protected ProcessTargetBuilder(ProcessBuilder step)
    {
        Verify.NotNull(step);
        this.Step = step;
    }

    /// <summary>
    /// Builds the function target.
    /// </summary>
    /// <returns>An instance of <see cref="KernelProcessFunctionTarget"/></returns>
    internal abstract KernelProcessTarget Build();

    /// <summary>
    /// An instance of <see cref="ProcessStepBuilder"/> representing the target Step.
    /// </summary>
    public ProcessStepBuilder Step { get; init; }
}

/// <summary>
/// Provides functionality for incrementally defining a process event target.
/// </summary>
public sealed class ProcessEventTargetBuilder: ProcessTargetBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessEventTargetBuilder"/> class.
    /// </summary>
    /// <param name="step"></param>
    /// <param name="eventName"></param>
    public ProcessEventTargetBuilder(ProcessStepBuilder step, string eventName)
        : base(step)
    {
        Verify.NotNull(step);
        this.Step = step;
        this.EventName = eventName;
    }

    internal override KernelProcessTarget Build()
    {
        Verify.NotNull(this.Step.Id);
        return new KernelProcessEventTarget(this.Step.Id, this.EventName);
    }

    /// <summary>
    /// The name of the Event to target.
    /// </summary>
    public string EventName { get; init; }
}

/// <summary>
/// Provides functionality for incrementally defining a process function target.
/// </summary>
public sealed class ProcessFunctionTargetBuilder : ProcessTargetBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessFunctionTargetBuilder"/> class.
    /// </summary>
    /// <param name="step">The step to target.</param>
    /// <param name="functionName">The function to target.</param>
    /// <param name="parameterName">The parameter to target.</param>
    public ProcessFunctionTargetBuilder(ProcessStepBuilder step, string? functionName = null, string? parameterName = null)
        : base(step)
    {
        Verify.NotNull(step);
        this.Step = step;

        // If the step is an EndStep, we don't need to resolve the function target.
        if (step is EndStep)
        {
            this.FunctionName = "END";
            this.ParameterName = null;
            return;
        }

        // Make sure the function target is valid.
        var target = step.ResolveFunctionTarget(functionName, parameterName);
        Verify.NotNull(target);

        this.FunctionName = target.FunctionName!;
        this.ParameterName = target.ParameterName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessFunctionTargetBuilder"/> class with an instance of <see cref="ProcessBuilder"/>
    /// as the step. This is used when the target is another process.
    /// </summary>
    /// <param name="step"></param>
    public ProcessFunctionTargetBuilder(ProcessBuilder step)
        : base(step)
    {
        Verify.NotNull(step);
        this.Step = step;
        this.FunctionName = "HandleExternalEvent";
        this.ParameterName = "externalEvent";
    }

    /// <summary>
    /// Builds the function target.
    /// </summary>
    /// <returns>An instance of <see cref="KernelProcessFunctionTarget"/></returns>
    internal override KernelProcessTarget Build()
    {
        Verify.NotNull(this.Step.Id);
        return new KernelProcessFunctionTarget(this.Step.Id, this.FunctionName, this.ParameterName);
    }

    /// <summary>
    /// The name of the function to target.
    /// </summary>
    public string FunctionName { get; init; }

    /// <summary>
    /// The name of the parameter to target. This may be null if the function has no parameters.
    /// </summary>
    public string? ParameterName { get; init; }
}
