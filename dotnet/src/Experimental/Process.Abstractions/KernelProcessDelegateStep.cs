// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using static Microsoft.SemanticKernel.KernelProcess;

namespace Microsoft.SemanticKernel;
/// <summary>
/// Step in a process that represents an ObjectModel.
/// </summary>
public class KernelDelegateProcessStep : KernelProcessStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelDelegateProcessStep"/> class with the specified step function.
    /// </summary>
    /// <param name="stepFunction"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public KernelDelegateProcessStep(StepFunction stepFunction)
    {
        this.StepFunction = stepFunction ?? throw new ArgumentNullException(nameof(stepFunction));
    }

    /// <summary>
    /// The step function delegate that will be invoked when the step is executed.
    /// </summary>
    public StepFunction StepFunction { get; init; } = null!;

    /// <summary>
    /// Invokes the step function with the provided kernel and context.
    /// </summary>
    /// <param name="kernel"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [KernelFunction("Invoke")]
    public async Task InvokeAsync(Kernel kernel, KernelProcessStepContext context)
    {
        await this.StepFunction(kernel, context).ConfigureAwait(false);
    }
}
