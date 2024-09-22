// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel;

/// <summary>
/// An abstract class representing an event that can be emitted from a <see cref="ProcessStep"/>.
/// </summary>
public class ProcessEvent
{
    /// <summary>
    /// The unique identifier for the event.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// An optional data payload associated with the event.
    /// </summary>
    public object? Data { get; set; }
}
