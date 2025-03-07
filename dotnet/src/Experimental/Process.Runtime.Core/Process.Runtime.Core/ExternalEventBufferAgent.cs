// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Process.Serialization;

namespace Process.Runtime.Core;

[TypeSubscription("default")]
public class ExternalEventBufferAgent : BaseAgent
{
    private readonly List<string> _queue = [];

    public ExternalEventBufferAgent(AgentId id, IAgentRuntime runtime, string description, ILogger<BaseAgent>? logger = null) : base(id, runtime, description, logger)
    {
    }

    /// <summary>
    /// Dequeues an event.
    /// </summary>
    /// <returns>A <see cref="List{T}"/> where T is <see cref="ProcessEvent"/></returns>
    public Task<DequeueMessageResponse> HandleAsync(DequeueMessage request)
    {
        // Dequeue and clear the queue.
        var response = new DequeueMessageResponse();
        response.Messages.AddRange([.. this._queue]);
        this._queue!.Clear();

        // Save the state.
        //await this.StateManager.SetStateAsync(ActorStateKeys.ExternalEventQueueState, this._queue).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);

        return Task.FromResult(response);
    }

    public Task HandleAsync(EnqueueMessage message)
    {
        this._queue.Add(message.Content);

        // Save the state.
        //await this.StateManager.SetStateAsync(ActorStateKeys.ExternalEventQueueState, this._queue).ConfigureAwait(false);
        //await this.StateManager.SaveStateAsync().ConfigureAwait(false);

        return Task.CompletedTask;
    }
}
