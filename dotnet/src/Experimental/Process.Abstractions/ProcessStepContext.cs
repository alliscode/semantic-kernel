// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides step related functionality for Kernel Functions running in a step.
/// </summary>
public class ProcessStepContext
{
    private readonly StepMessageChannel _stepMessageChannel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStepContext"/> class.
    /// </summary>
    /// <param name="channel"></param>
    public ProcessStepContext(StepMessageChannel channel)
    {
        this._stepMessageChannel = channel;
    }

    /// <summary>
    /// Emit an event from the current step.
    /// </summary>
    /// <param name="processEvent"></param>
    /// <returns></returns>
    public ValueTask EmitEventAsync(ProcessEvent processEvent)
    {
        return this._stepMessageChannel.EmitEventAsync(processEvent);
    }
}
