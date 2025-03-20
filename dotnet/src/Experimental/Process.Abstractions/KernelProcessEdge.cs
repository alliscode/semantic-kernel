// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.Serialization;

namespace Microsoft.SemanticKernel;

/// <summary>
/// A serializable representation of an edge between a source <see cref="KernelProcessStep"/> and a <see cref="KernelProcessFunctionTarget"/>.
/// </summary>
[DataContract]
[KnownType(typeof(KernelProcessFunctionTarget))]
public sealed class KernelProcessEdge
{
    /// <summary>
    /// The unique identifier of the source Step.
    /// </summary>
    [DataMember]
    public string SourceStepId { get; init; }

    /// <summary>
    /// The collection of <see cref="KernelProcessFunctionTarget"/>s that are the output of the source Step.
    /// </summary>
    [DataMember]
    public KernelProcessFunctionTarget OutputTarget { get; init; }

    /// <summary>
    /// The collection of extra data associated with the edge.
    /// </summary>
    [DataMember]
    public KernelProcessEdgeProxyInfo? ProxyInfo { get; init; }

    /// <summary>
    /// Creates a new instance of the <see cref="KernelProcessEdge"/> class.
    /// </summary>
    public KernelProcessEdge(string sourceStepId, KernelProcessFunctionTarget outputTarget, KernelProcessEdgeProxyInfo? proxyInfo = null)
    {
        Verify.NotNullOrWhiteSpace(sourceStepId);
        Verify.NotNull(outputTarget);

        this.SourceStepId = sourceStepId;
        this.OutputTarget = outputTarget;
        this.ProxyInfo = proxyInfo;
    }
}
