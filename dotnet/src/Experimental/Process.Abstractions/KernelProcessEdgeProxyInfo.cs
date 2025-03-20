// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.SemanticKernel;

/// <summary>
/// Represents a message used in a process runtime.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KernelProcessEdgeProxyInfo"/> class.
/// </remarks>
/// <param name="TopicId">The Id of the topic the edge is targeting.</param>
/// <param name="ChannelKey">The key associated with the messaging channel that the edge is targeting.</param>
public record KernelProcessEdgeProxyInfo(
    string TopicId,
    string? ChannelKey)
{
}
