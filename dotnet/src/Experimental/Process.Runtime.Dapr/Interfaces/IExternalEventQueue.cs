// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Actors;

namespace Microsoft.SemanticKernel;
internal interface IExternalEventQueue : IActor
{
    ValueTask EnqueueAsync(KernelProcessEvent externalEvent);

    Task<List<KernelProcessEvent>> DequeueAllAsync();
}
