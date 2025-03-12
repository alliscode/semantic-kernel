// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.Serialization;
using Process.Runtime.Core;

namespace Microsoft.SemanticKernel;

/// <summary>
/// A context for a Dapr kernel process.
/// </summary>
public class CoreKernelProcessContext : KernelProcessContext
{
    private readonly KernelProcess _process;
    private readonly AgentId _processAgentId;
    private readonly InProcessRuntime _agentRuntime;

    internal CoreKernelProcessContext(KernelProcess process)
    {
        Verify.NotNull(process);
        Verify.NotNullOrWhiteSpace(process.State?.Name);

        if (string.IsNullOrWhiteSpace(process.State.Id))
        {
            process = process with { State = process.State with { Id = Guid.NewGuid().ToString() } };
        }

        this._process = process;
        this._processAgentId = new AgentId(nameof(CoreProcess), process.State.Id);
        this._agentRuntime = new InProcessRuntime();
    }

    private async Task RegisterRuntimeAgentsAsync()
    {
        await this._agentRuntime.RegisterAgentFactoryAsync(
            type: nameof(CoreProcess), (AgentId id, IAgentRuntime runtime) =>
            {
                return ValueTask.FromResult<IHostableAgent>(new CoreProcess(id, runtime, new Kernel()));
            }).ConfigureAwait(false);

        await this._agentRuntime.RegisterImplicitAgentSubscriptionsAsync(nameof(CoreProcess), typeof(CoreProcess)).ConfigureAwait(false);

        await this._agentRuntime.RegisterAgentFactoryAsync(
            type: nameof(CoreStep), (AgentId id, IAgentRuntime runtime) =>
            {
                return ValueTask.FromResult<IHostableAgent>(new CoreStep(id, runtime, new Kernel()));
            }).ConfigureAwait(false);

        await this._agentRuntime.RegisterImplicitAgentSubscriptionsAsync(nameof(CoreStep), typeof(CoreStep)).ConfigureAwait(false);

        await this._agentRuntime.RegisterAgentFactoryAsync(
            type: nameof(MessageBufferAgent), (AgentId id, IAgentRuntime runtime) =>
            {
                return ValueTask.FromResult<IHostableAgent>(new MessageBufferAgent(id, runtime, ""));
            }).ConfigureAwait(false);

        await this._agentRuntime.RegisterImplicitAgentSubscriptionsAsync<MessageBufferAgent>(nameof(EventBufferAgent)).ConfigureAwait(false);

        await this._agentRuntime.RegisterAgentFactoryAsync(
            type: nameof(EventBufferAgent), (AgentId id, IAgentRuntime runtime) =>
            {
                return ValueTask.FromResult<IHostableAgent>(new EventBufferAgent(id, runtime, ""));
            }).ConfigureAwait(false);

        await this._agentRuntime.RegisterImplicitAgentSubscriptionsAsync<EventBufferAgent>(nameof(EventBufferAgent)).ConfigureAwait(false);

        await this._agentRuntime.RegisterAgentFactoryAsync(
            type: nameof(ExternalEventBufferAgent), (AgentId id, IAgentRuntime runtime) =>
            {
                return ValueTask.FromResult<IHostableAgent>(new ExternalEventBufferAgent(id, runtime, ""));
            }).ConfigureAwait(false);

        await this._agentRuntime.RegisterImplicitAgentSubscriptionsAsync<ExternalEventBufferAgent>(nameof(ExternalEventBufferAgent)).ConfigureAwait(false);

        await this._agentRuntime.RegisterAgentFactoryAsync(
            type: nameof(ExternalMessageBufferAgent), (AgentId id, IAgentRuntime runtime) =>
            {
                return ValueTask.FromResult<IHostableAgent>(new ExternalMessageBufferAgent(id, runtime, ""));
            }).ConfigureAwait(false);

        await this._agentRuntime.RegisterImplicitAgentSubscriptionsAsync<ExternalMessageBufferAgent>(nameof(ExternalMessageBufferAgent)).ConfigureAwait(false);

        await this._agentRuntime.StartAsync().ConfigureAwait(false);

    }

    /// <summary>
    /// Starts the process with an initial event.
    /// </summary>
    /// <param name="initialEvent">The initial event.</param>
    /// <param name="eventProxyStepId">An optional identifier of an actor requesting to proxy events.</param>
    internal async Task StartWithEventAsync(KernelProcessEvent initialEvent, AgentId? eventProxyStepId = null)
    {
        await this.RegisterRuntimeAgentsAsync().ConfigureAwait(false);

        //var daprProcess = DaprProcessInfo.FromKernelProcess(this._process);
        //await this._daprProcess.InitializeProcessAsync(daprProcess, null, eventProxyStepId?.GetId()).ConfigureAwait(false);
        //await this._daprProcess.RunOnceAsync(initialEvent.ToJson()).ConfigureAwait(false);

        var process = this._process.ToProcessStepInfo();

        await this._agentRuntime.SendMessageAsync(
            new InitializeStep
            {
                StepInfo = process,
                EventProxyStepId = eventProxyStepId?.Key ?? string.Empty,
            },
            this._processAgentId
            ).ConfigureAwait(false);

        await this._agentRuntime.SendMessageAsync(
            new RunOnce
            {
                Event = initialEvent.ToJson()
            },
            this._processAgentId
            ).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a message to the process.
    /// </summary>
    /// <param name="processEvent">The event to sent to the process.</param>
    /// <returns>A <see cref="Task"/></returns>
    public override async Task SendEventAsync(KernelProcessEvent processEvent) =>
        await this._agentRuntime.SendMessageAsync(
            new SendMessage
            {
                Event = processEvent.ToJson()
            },
            this._processAgentId
            ).ConfigureAwait(false);

    /// <summary>
    /// Stops the process.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    public override Task StopAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a snapshot of the current state of the process.
    /// </summary>
    /// <returns>A <see cref="Task{T}"/> where T is <see cref="KernelProcess"/></returns>
    public override async Task<KernelProcess> GetStateAsync()
    {
        var result = await this._agentRuntime.SendMessageAsync(
            new ToProcessStepInfo(),
            this._processAgentId
            ).ConfigureAwait(false);

        if (result is not ProcessStepInfo processInfo)
        {
            throw new KernelException($"Unable to get process state from {nameof(CoreKernelProcessContext)}");
        }

        return processInfo.ToKernelProcess();
    }

    /// <inheritdoc/>
    public override Task<IExternalKernelProcessMessageChannel?> GetExternalMessageChannelAsync()
    {
        throw new NotImplementedException();
    }
}
