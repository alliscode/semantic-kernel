// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Microsoft.SemanticKernel.Process;
internal class LocalEventProxyChannel : IExternalKernelProcessMessageChannel
{
    private HubConnection? _hubConnection;

    public Task EmitExternalEventAsync(string externalTopicEvent, KernelProcessProxyMessage eventData)
    {
        if (this._hubConnection == null)
        {
            throw new InvalidOperationException("Hub connection is not initialized.");
        }

        return this._hubConnection.InvokeAsync("HandleEventAsync", JsonSerializer.Serialize(eventData));
    }

    public async ValueTask Initialize()
    {
        this._hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost:5000/events"))
            .Build();

        await this._hubConnection.StartAsync().ConfigureAwait(false);
    }

    public async ValueTask Uninitialize()
    {
        if (this._hubConnection == null)
        {
            throw new InvalidOperationException("Hub connection is not initialized.");
        }

        await this._hubConnection.StopAsync().ConfigureAwait(false);
    }
}
