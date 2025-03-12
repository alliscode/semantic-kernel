// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Process.Serialization;

namespace Process.Runtime.Core;
internal class MessageBufferAgent : BaseAgent, IHandle<DequeueMessage, DequeueMessageResponse>, IHandle<EnqueueMessage>
{
    private readonly List<string> _queue = [];

    public MessageBufferAgent(AgentId id, IAgentRuntime runtime, string description, ILogger<MessageBufferAgent>? logger = null) : base(id, runtime, description, logger)
    {
    }

    public async ValueTask HandleAsync(EnqueueMessage message, MessageContext messageContext)
    {
        this._queue.Add(message.Content);

        // Save the state.
        //await this.StateManager.SetStateAsync(ActorStateKeys.MessageQueueState, this._queue).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    public async ValueTask<DequeueMessageResponse> HandleAsync(DequeueMessage message, MessageContext messageContext)
    {
        if (this._queue.Count > 0)
        {
            int x = 3;
        }

        // Dequeue and clear the queue.
        string[] items = [.. this._queue];
        this._queue.Clear();

        // Save the state.
        //await this.StateManager.SetStateAsync(ActorStateKeys.MessageQueueState, this._queue).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);

        var response = new DequeueMessageResponse();
        response.Messages.AddRange(items);
        return response;
    }
}
