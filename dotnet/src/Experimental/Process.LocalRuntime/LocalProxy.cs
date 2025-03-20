// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Runtime;

namespace Microsoft.SemanticKernel;

internal sealed class LocalProxy : LocalStep
{
    private readonly KernelProcessProxy _proxy;
    private readonly IReadOnlyDictionary<string, IExternalKernelProcessMessageChannel> _externalMessageChannels;
    private readonly ILogger _logger;

    private bool _isInitialized = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalMap"/> class.
    /// </summary>
    /// <param name="proxy">an instance of <see cref="KernelProcessProxy"/></param>
    /// <param name="kernel">An instance of <see cref="Kernel"/></param>
    internal LocalProxy(KernelProcessProxy proxy, Kernel kernel, IReadOnlyDictionary<string, IExternalKernelProcessMessageChannel> externalMessageChannels)
        : base(proxy, kernel)
    {
        this._proxy = proxy;
        this._logger = this._kernel.LoggerFactory?.CreateLogger(this._proxy.State.Name) ?? new NullLogger<LocalStep>();
        this._externalMessageChannels = externalMessageChannels;
    }

    internal override async Task HandleMessageAsync(ProcessMessage message)
    {
        Verify.NotNull(message, nameof(message));

        // Lazy one-time initialization of the step before processing a message
        await this._activateTask.Value.ConfigureAwait(false);

        if (this._externalMessageChannels.Count == 0)
        {
            throw new KernelException("The proxy step requires a channel id be provided").Log(this._logger);
        }

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

    /// <inheritdoc/>
    protected override async ValueTask InitializeStepAsync()
    {
        if (this._isInitialized)
        {
            return;
        }

        // Ensure initialization happens only once if first time or again if "deinitialization" was called
        if (this.ExternalMessageChannel == null)
        {
            throw new KernelException("No IExternalKernelProcessMessageChannel found, need at least 1 to emit external messages");
        }

        await this.ExternalMessageChannel.Initialize().ConfigureAwait(false);
        await base.InitializeStepAsync().ConfigureAwait(false);
        this._isInitialized = true;
    }

    /// <summary>
    /// Deinitialization of the Proxy Step, calling <see cref="KernelProxyStep.DeactivateAsync(KernelProcessStepExternalContext)"/>
    /// </summary>
    /// <returns></returns>
    public override async Task DeinitializeStepAsync()
    {
        MethodInfo? derivedMethod = this._stepInfo.InnerStepType.GetMethod(
            nameof(KernelProxyStep.DeactivateAsync),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(KernelProcessStepExternalContext)],
            modifiers: null);

        if (derivedMethod != null && this._stepInstance != null)
        {
            var context = new KernelProcessStepExternalContext(this.ExternalMessageChannel);
            ValueTask deactivateTask =
                (ValueTask?)derivedMethod.Invoke(this._stepInstance, [context]) ??
                throw new KernelException($"The derived DeactivateAsync method failed to complete for step {this.Name}.").Log(this._logger);

            await deactivateTask.ConfigureAwait(false);
        }
    }
}
