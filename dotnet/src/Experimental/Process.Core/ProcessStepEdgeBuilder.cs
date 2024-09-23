// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process edge.
/// </summary>
public class ProcessStepEdgeBuilder
{
    private ProcessFunctionTargetBuilder? _outputTarget;
    private readonly string _eventId;

    internal ProcessStepBuilder Source { get; init; }

    internal ProcessStepEdgeBuilder(ProcessStepBuilder source, string eventType)
    {
        this.Source = source;
        this._eventId = eventType;
    }

    /// <summary>
    /// Sends the output of the source step to the specified target when the associated event fires.
    /// </summary>
    /// <param name="outputTarget">The output target.</param>
    public void SendEventTo(ProcessFunctionTargetBuilder outputTarget)
    {
        this._outputTarget = outputTarget;
        this.Source.LinkTo(this._eventId, this);
    }

    /// <summary>
    /// Sends a message to stop the process.
    /// </summary>
    public void StopProcess()
    {
        var outputTarget = new ProcessFunctionTargetBuilder(EndStep.Instance);
        this._outputTarget = outputTarget;
        this.Source.LinkTo("STOP", this);
    }

    /// <summary>
    /// Builds the edge.
    /// </summary>
    public KernelProcessEdge Build()
    {
        return new KernelProcessEdge(this.Source.Id, this._outputTarget.Build());
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
    public void SendEventTo(ProcessStepEdgeBuilder outputTarget)
    {
        this._source.LinkTo(this._eventId, outputTarget);
    }
}
