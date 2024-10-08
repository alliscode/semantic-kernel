// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;
internal record DaprEvent
{
    /// <summary>
    /// The inner <see cref="KernelProcessEvent"/> that this <see cref="DaprEvent"/> wraps.
    /// </summary>
    private KernelProcessEvent InnerEvent { get; init; }

    /// <summary>
    /// The namespace of the event.
    /// </summary>
    internal required string? Namespace { get; init; }

    /// <summary>
    /// The Id of the event.
    /// </summary>
    internal string Id => $"{this.Namespace}.{this.InnerEvent.Id}";

    /// <summary>
    /// The data of the event.
    /// </summary>
    internal object? Data => this.InnerEvent.Data;

    /// <summary>
    /// The visibility of the event.
    /// </summary>
    internal KernelProcessEventVisibility Visibility => this.InnerEvent.Visibility;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprEvent"/> class.
    /// </summary>
    /// <param name="eventNamespace">The namespace of the event.</param>
    /// <param name="innerEvent">The instance of <see cref="KernelProcessEvent"/> that this <see cref="DaprEvent"/> came from.</param>
    internal DaprEvent(string? eventNamespace, KernelProcessEvent innerEvent)
    {
        this.Namespace = eventNamespace;
        this.InnerEvent = innerEvent;
    }

    /// <summary>
    /// Creates a new <see cref="DaprEvent"/> from a <see cref="KernelProcessEvent"/>.
    /// </summary>
    /// <param name="kernelProcessEvent">The <see cref="KernelProcessEvent"/></param>
    /// <param name="Namespace">The namespace of the event.</param>
    /// <returns>An instance of <see cref="DaprEvent"/></returns>
    internal static DaprEvent FromKernelProcessEvent(KernelProcessEvent kernelProcessEvent, string Namespace) => new(Namespace, kernelProcessEvent);
}
