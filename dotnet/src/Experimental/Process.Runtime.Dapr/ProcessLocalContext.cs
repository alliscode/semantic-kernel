// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Process;
public sealed class ProcessLocalContext
{
    private readonly ConcurrentDictionary<string, Func<KernelProcessProxyMessage, Task>> _callbacks = [];

    public ProcessLocalContext()
    {
    }

    public void RegisterCallback(string processId, Func<KernelProcessProxyMessage, Task> callback)
    {
        Verify.NotNullOrWhiteSpace(processId, nameof(processId));
        Verify.NotNull(callback);

        this._callbacks.TryAdd(this.CreateCallbackKey(processId, ""), callback);
    }

    public async Task HandleEventAsync(KernelProcessProxyMessage message)
    {
        Verify.NotNull(message);

        if (!this._callbacks.TryGetValue(this.CreateCallbackKey(message.ProcessId, ""), out var action))
        {
            throw new KernelException("");
        }

        await action(message).ConfigureAwait(false);
    }

    private string CreateCallbackKey(string procesId, string eventId)
    {
        return procesId + "_" + eventId;
    }
}
