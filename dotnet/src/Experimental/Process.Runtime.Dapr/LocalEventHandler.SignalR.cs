// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.SemanticKernel.Process;
public class LocalEventHandler : Hub
{
    public Task HandleEventAsync(string eventData)
    {
        return Task.CompletedTask;
    }
}
