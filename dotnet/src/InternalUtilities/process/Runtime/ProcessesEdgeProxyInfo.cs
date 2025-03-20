// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.SemanticKernel.Process.Runtime;

/// <summary>
/// Represents a message used in a process runtime.
/// </summary>
public class ProcessEdgeProxyInfo
{
    /// <summary>
    /// The Id of the topic the edge is targeting.
    /// </summary>
    public string TopicId { get; }

    /// <summary>
    /// The key associated with the messaging channel that the edge is targeting.
    /// </summary>
    public string? ChannelKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessEdgeProxyInfo"/> class.
    /// </summary>
    /// <param name="topicId">The Id of the topic the edge is targeting.</param>
    /// <param name="channelKey">The key associated with the messaging channel that the edge is targeting.</param>
    public ProcessEdgeProxyInfo(string topicId, string? channelKey)
    {
        this.TopicId = topicId;
        this.ChannelKey = channelKey;
    }

    /// <summary>
    /// Converts this instance to a <see cref="KernelProcessEdgeProxyInfo"/> instance.
    /// </summary>
    /// <returns></returns>
    public KernelProcessEdgeProxyInfo ToKernelProcessEdgeProxyInfo()
    {
        return new KernelProcessEdgeProxyInfo(this.TopicId, this.ChannelKey);
    }

    /// <summary>
    /// Converts a <see cref="KernelProcessEdgeProxyInfo"/> instance to a <see cref="ProcessEdgeProxyInfo"/> instance.
    /// </summary>
    /// <param name="proxyInfo">The <see cref="KernelProcessEdgeProxyInfo"/> to build from.</param>
    /// <returns></returns>
    public static ProcessEdgeProxyInfo? FromKernelProcessEdgeProxyInfo(KernelProcessEdgeProxyInfo? proxyInfo)
    {
        return proxyInfo is null ? null : new ProcessEdgeProxyInfo(proxyInfo.TopicId, proxyInfo.ChannelKey);
    }
}
