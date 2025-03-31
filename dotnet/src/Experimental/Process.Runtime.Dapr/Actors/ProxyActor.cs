// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Runtime;

namespace Microsoft.SemanticKernel;

internal sealed class ProxyActor : StepActor, IProxy
{
    private readonly ILogger? _logger;
    private readonly IReadOnlyDictionary<string, IExternalKernelProcessMessageChannel> _externalMessageChannels;

    internal DaprProxyInfo? _daprProxyInfo;


    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyActor"/> class.
    /// </summary>
    /// <param name="host">The Dapr host actor</param>
    /// <param name="kernel">An instance of <see cref="Kernel"/></param>
    /// <param name="externalMessageChannels">The external message channels</param>
    public ProxyActor(ActorHost host, Kernel kernel, IReadOnlyDictionary<string, IExternalKernelProcessMessageChannel> externalMessageChannels)
        : base(host, kernel)
    {
        this._logger = this._kernel.LoggerFactory?.CreateLogger(typeof(KernelProxyStep)) ?? new NullLogger<ProxyActor>();
        this._externalMessageChannels = externalMessageChannels;
    }

    internal override async Task HandleMessageAsync(ProcessMessage message)
    {
        Verify.NotNull(message, nameof(message));

        if (this._externalMessageChannels.Count == 0)
        {
            throw new KernelException("The proxy step requires a channel id be provided").Log(this._logger);
        }

        // Lazy one-time initialization of the step before processing a message
        await this._activateTask.Value.ConfigureAwait(false);

        if (this._functions is null || this._inputs is null || this._initialInputs is null)
        {
            throw new KernelException("The step has not been initialized.").Log(this._logger);
        }

        if (message.Values.Count != 1)
        {
            throw new KernelException("The proxy step can only handle 1 parameter object").Log(this._logger);
        }

        if (string.IsNullOrWhiteSpace(message.ProxyInfo?.TopicId))
        {
            throw new KernelException("The proxy step requires a topic id be provided").Log(this._logger);
        }

        IExternalKernelProcessMessageChannel? channel = null;

        // If there is no channelId provided, there can only be one channel
        if (string.IsNullOrEmpty(message.ProxyInfo.ChannelKey))
        {
            if (this._externalMessageChannels.Count > 1)
            {
                throw new KernelException("The proxy step requires a channel id be provided").Log(this._logger);
            }

            channel = this._externalMessageChannels.Single().Value;
        }
        else
        {
            channel = this._externalMessageChannels[message.ProxyInfo.ChannelKey];
        }

        if (channel is null)
        {
            throw new KernelException("The proxy step requires a channel id be provided").Log(this._logger);
        }

        // Add the message values to the inputs for the function
        var kvp = message.Values.Single();
        var proxyMessage = KernelProcessProxyMessageFactory.CreateProxyMessage(this.ParentProcessId!, message.SourceEventId, message.ProxyInfo.TopicId, kvp.Value);
        await channel.EmitExternalEventAsync(proxyMessage.ExternalTopicName, proxyMessage).ConfigureAwait(false);
    }

    public async Task InitializeProxyAsync(DaprProxyInfo proxyInfo, string? parentProcessId)
    {
        this._daprProxyInfo = proxyInfo;

        await base.InitializeStepAsync(proxyInfo, parentProcessId).ConfigureAwait(false);
    }

    protected async override ValueTask ActivateStepAsync()
    {
        await base.ActivateStepAsync().ConfigureAwait(false);

        foreach (var kvp in this._externalMessageChannels)
        {
            await kvp.Value.Initialize().ConfigureAwait(false);
        }
    }
}
