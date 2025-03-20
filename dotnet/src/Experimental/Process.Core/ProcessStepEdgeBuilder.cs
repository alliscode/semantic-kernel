// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel.Process.Internal;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process edge.
/// </summary>
public sealed class ProcessStepEdgeBuilder
{
    internal ProcessFunctionTargetBuilder? Target { get; set; }

    /// <summary>
    /// The event data that the edge fires on.
    /// </summary>
    internal ProcessEventData EventData { get; }

    /// <summary>
    /// The source step of the edge.
    /// </summary>
    internal ProcessStepBuilder Source { get; }

    /// <summary>
    /// The extras dictionary for the edge.
    /// </summary>
    internal KernelProcessEdgeProxyInfo? ProxyInfo { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStepEdgeBuilder"/> class.
    /// </summary>
    /// <param name="source">The source step.</param>
    /// <param name="eventId">The Id of the event.</param>
    /// <param name="eventName"></param>
    internal ProcessStepEdgeBuilder(ProcessStepBuilder source, string eventId, string eventName)
    {
        Verify.NotNull(source, nameof(source));
        Verify.NotNullOrWhiteSpace(eventId, nameof(eventId));

        this.Source = source;
        this.EventData = new() { EventId = eventId, EventName = eventName };
    }

    /// <summary>
    /// Builds the edge.
    /// </summary>
    internal KernelProcessEdge Build()
    {
        Verify.NotNull(this.Source?.Id);
        Verify.NotNull(this.Target);

        return new KernelProcessEdge(this.Source.Id, this.Target.Build(), proxyInfo: this.ProxyInfo);
    }

    /// <summary>
    /// Signals that the output of the source step should be sent to the specified target when the associated event fires.
    /// </summary>
    /// <param name="target">The output target.</param>
    /// <returns>A fresh builder instance for fluid definition</returns>
    public ProcessStepEdgeBuilder SendEventTo(ProcessFunctionTargetBuilder target)
    {
        if (this.Target is not null)
        {
            throw new InvalidOperationException("An output target has already been set.");
        }

        if (this.Source is ProcessMapBuilder && target.Step is ProcessMapBuilder)
        {
            throw new ArgumentException($"{nameof(ProcessMapBuilder)} may not target another {nameof(ProcessMapBuilder)}.", nameof(target));
        }

        this.Target = target;
        this.Source.LinkTo(this.EventData.EventId, this);

        return new ProcessStepEdgeBuilder(this.Source, this.EventData.EventId, this.EventData.EventName);
    }

    /// <summary>
    /// Emit the event to an external event channel using the specified topic name.
    /// </summary>
    /// <returns>An instance of <see cref="ProcessStepEdgeBuilder"/></returns>
    public ProcessStepEdgeBuilder EmitExternalEvent(string topicName, string? channelKey = null)
    {
        var processBuilder = this.Source.ProcessBuilder;

        this.ProxyInfo ??= new(TopicId: topicName, ChannelKey: channelKey);

        if (processBuilder is null)
        {
            throw new InvalidOperationException("The root process could not be found.");
        }

        var targetBuilder = processBuilder.ExternalProxyStep.GetExternalFunctionTargetBuilder();
        return this.SendEventTo(targetBuilder);
    }

    /// <summary>
    /// Emit the SK step event as an external event with specific topic name
    /// </summary>
    /// <returns>An instance of <see cref="ProcessStepEdgeBuilder"/></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ProcessStepEdgeBuilder EmitLocalEvent()
    {
        var processBuilder = this.Source.ProcessBuilder;

        this.ProxyInfo ??= new(TopicId: "LocalProxy", ChannelKey: "LocalProxy");

        if (processBuilder is null)
        {
            throw new InvalidOperationException("The root process could not be found.");
        }

        var targetBuilder = processBuilder.LocalProxyStep.GetExternalFunctionTargetBuilder();
        return this.SendEventTo(targetBuilder);
    }

    /// <summary>
    /// Signals that the process should be stopped.
    /// </summary>
    public void StopProcess()
    {
        if (this.Target is not null)
        {
            throw new InvalidOperationException("An output target has already been set.");
        }

        var outputTarget = new ProcessFunctionTargetBuilder(EndStep.Instance);
        this.Target = outputTarget;
        this.Source.LinkTo(ProcessConstants.EndStepName, this);
    }
}
