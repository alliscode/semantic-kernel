// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Process.Interfaces;

namespace Microsoft.SemanticKernel;
internal class EventPollActor : Actor, IEventPoll
{
    private readonly Channel<string> _channel;
    private readonly ILogger? _logger;

    public EventPollActor(ActorHost host, ILogger<EventPollActor> logger) : base(host)
    {
        this._channel = Channel.CreateUnbounded<string>();
        this._logger = logger;
    }

    public Task EnqueueAsync(string stepEvent)
    {
        this._logger?.LogWarning("{Time}: Enqueuing event: {Event}", DateTime.Now.ToString("T"), stepEvent);
        this._channel.Writer.TryWrite(stepEvent);
        return Task.CompletedTask;
    }

    public async Task<IList<string>> GetAvailableAsync()
    {
        this._logger?.LogWarning("{Time}: Dequeueing events", DateTime.Now.ToString("T"));

        try
        {
            var events = await ReadWithTimeoutAsync(
                this._channel.Reader,
                TimeSpan.FromSeconds(5), // Timeout after 5 seconds
                CancellationToken.None
            ).ToListAsync().ConfigureAwait(false);

            return events;
        }
        catch (TimeoutException ex)
        {
            return new List<string>(); // Return an empty list if timeout occurs
        }
    }

    private static IAsyncEnumerable<T> ReadWithTimeoutAsync<T>(ChannelReader<T> reader, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using (var timeoutCts = new CancellationTokenSource(timeout))
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
        {
            try
            {
                return reader.ReadAllAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }
    }
}
