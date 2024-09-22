// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process edge.
/// </summary>
public class ProcessStepEdgeBuilder
{
    private ProcessStepBuilder _source { get; }
    private readonly string _eventId;

    internal ProcessStepEdgeBuilder(ProcessStepBuilder source, string eventType)
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

    /// <summary>
    /// Sends a message to stop the process.
    /// </summary>
    public void StopProcess()
    {
        this._source.LinkTo("STOP", new ProcessFunctionTargetBuilder(EndStep.Instance));
    }
}

/// <summary>
/// Provides functionality for incrementally defining a process edge.
/// </summary>
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
