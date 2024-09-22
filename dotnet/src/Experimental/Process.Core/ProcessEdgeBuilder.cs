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

    public void SendOutputTo(OutputTarget2 outputTarget)
    {
        this._source.LinkTo(this._eventType, outputTarget);
    }

    public void SendOutputTo(ProcessStepBuilder step, string? function = null, string? parameter = null)
    {
        this._source.LinkTo(this._eventType, new OutputTarget2(step, function, parameter));
    }
}
