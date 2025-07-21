// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


/// <summary>
/// Simplified message structure for step communication.
/// </summary>
internal sealed record StepMessage
{
    /// <summary>
    /// The source step that sent this message.
    /// </summary>
    public string SourceId { get; init; } = string.Empty;

    /// <summary>
    /// The destination step for this message.
    /// </summary>
    public string DestinationId { get; init; } = string.Empty;

    /// <summary>
    /// The target function name to invoke.
    /// </summary>
    public string FunctionName { get; init; } = string.Empty;

    /// <summary>
    /// The event ID that triggered this message.
    /// </summary>
    public string? SourceEventId { get; init; }

    /// <summary>
    /// The target event ID for sub-processes.
    /// </summary>
    public string? TargetEventId { get; init; }

    /// <summary>
    /// The data payload for the message.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// The function parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Optional group ID for edge group processing.
    /// </summary>
    public string? GroupId { get; init; }

    /// <summary>
    /// Thread ID for agent steps.
    /// </summary>
    public string? ThreadId { get; init; }
}