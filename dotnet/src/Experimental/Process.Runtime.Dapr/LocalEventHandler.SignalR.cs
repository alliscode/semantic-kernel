// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.SemanticKernel.Process;
public class LocalEventHandler : Hub
{
    private readonly ProcessLocalContext _processLocalContext;

    public LocalEventHandler(ProcessLocalContext processLocalContext)
    {
        _processLocalContext = processLocalContext;
    }

    public async Task HandleEventAsync(string eventData)
    {
        var message = JsonSerializer.Deserialize<KernelProcessProxyMessage>(eventData);
        await this._processLocalContext.HandleEventAsync(message).ConfigureAwait(false);
    }
}
