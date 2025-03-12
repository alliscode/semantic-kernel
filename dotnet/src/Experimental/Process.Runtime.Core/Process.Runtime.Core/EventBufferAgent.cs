// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Process.Serialization;

namespace Process.Runtime.Core;
public class EventBufferAgent : BaseAgent, IHandle<DequeueMessage, DequeueMessageResponse>, IHandle<EnqueueMessage>
{
    private readonly List<string> _queue = [];

    public EventBufferAgent(AgentId id, IAgentRuntime runtime, string description, ILogger<BaseAgent>? logger = null) : base(id, runtime, description, logger)
    {
    }

    public async Task EnqueueAsync(EnqueueMessage message)
    {
        this._queue.Add(message.Content);

        // Save the state.
        //await this.StateManager.SetStateAsync(ActorStateKeys.ExternalEventQueueState, this._queue).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    public ValueTask<DequeueMessageResponse> HandleAsync(DequeueMessage item, MessageContext messageContext)
    {
        // Dequeue and clear the queue.
        var response = new DequeueMessageResponse();
        response.Messages.AddRange([.. this._queue]);
        this._queue!.Clear();

        // Save the state.
        //await this.StateManager.SetStateAsync(ActorStateKeys.ExternalEventQueueState, this._queue).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);

        return ValueTask.FromResult(response);
    }

    public ValueTask HandleAsync(EnqueueMessage item, MessageContext messageContext)
    {
        this._queue.Add(item.Content);

        // Save the state.
        //await this.StateManager.SetStateAsync(ActorStateKeys.ExternalEventQueueState, this._queue).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);

        return ValueTask.CompletedTask;
    }
}
