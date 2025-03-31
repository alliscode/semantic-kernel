// Copyright (c) Microsoft. All rights reserved.
using System.Runtime.Serialization;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Represents a message used in a process runtime.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KernelProcessEdgeProxyInfo"/> class.
/// </remarks>
/// <param name="TopicId">The Id of the topic the edge is targeting.</param>
/// <param name="ChannelKey">The key associated with the messaging channel that the edge is targeting.</param>
[DataContract]
public class KernelProcessEdgeProxyInfo
{
    [DataMember]
    public string TopicId { get; set; }

    [DataMember]
    public string? ChannelKey { get; set; }

    public KernelProcessEdgeProxyInfo(string topicId, string? channelKey)
    {
        this.TopicId = topicId;
        this.ChannelKey = channelKey;
    }
}
