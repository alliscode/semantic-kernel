// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process edge.
/// </summary>
public class ProcessStepEdgeBuilder
{
    internal ProcessStepBuilder Source { get; }
    private readonly string _eventId;

    internal ProcessStepEdgeBuilder(ProcessStepBuilder source, string eventType)
    {
        this.Source = source;
        this._eventId = eventType;
    }

    /// <summary>
    /// Sends the output of the source step to the specified target when the associated event fires.
    /// </summary>
    /// <param name="outputTarget">The output target.</param>
    public void SendOutputTo(ProcessFunctionTargetBuilder outputTarget)
    {
        this.Source.LinkTo(this._eventId, outputTarget);
    }
}

public class ProcessEdgeBuilder
{
    private readonly ProcessBuilder _source;
    private readonly string _eventId;

    internal ProcessEdgeBuilder(ProcessBuilder source, string eventType)
    {
        this._source = source;
        this._eventId = eventType;
    }

    /// <summary>
    /// Sends the output of the source step to the specified target when the associated event fires.
    /// </summary>
    /// <param name="outputTarget">The output target.</param>
    public void SendEventTo(ProcessFunctionTargetBuilder outputTarget)
    {
        this._source.LinkTo(this._eventId, outputTarget);
    }
}
