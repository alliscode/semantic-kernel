// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process edge.
/// </summary>
public class ProcessEdgeBuilder
{
    private readonly ProcessStepBuilder _source;

    private readonly string _eventType;

    public ProcessEdgeBuilder(ProcessStepBuilder source, string eventType)
    {
        this._source = source;
        this._eventType = eventType;
    }

    public void SendOutputTo(ProcessFunctionTargetBuilder outputTarget)
    {
        this._source.LinkTo(this._eventType, outputTarget);
    }

    public void SendOutputTo<TTargetStep>(ProcessStepBuilder<TTargetStep> step, string? function = null, string? parameter = null)
        where TTargetStep : ProcessStepBase
    {
        this._source.LinkTo(this._eventType, new ProcessFunctionTargetBuilder(step, function, parameter));
    }
}
