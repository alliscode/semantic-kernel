// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.SemanticKernel.Process;
public sealed class KeyedExternalChannels(IServiceCollection sc)
{
    public string[] Keys { get; } = (
        from service in sc
        where service.ServiceKey != null
        where service.ServiceKey!.GetType() == typeof(string)
        where service.ServiceType == typeof(IExternalKernelProcessMessageChannel)
        select (string)service.ServiceKey!)
        .ToArray();
}

public sealed class KeyedServiceDictionary(
        KeyedExternalChannels keys, IServiceProvider provider)
        : ReadOnlyDictionary<string, IExternalKernelProcessMessageChannel>(Create(keys, provider))
{
    private static Dictionary<string, IExternalKernelProcessMessageChannel> Create(
        KeyedExternalChannels keys, IServiceProvider provider)
    {
        var dict = new Dictionary<string, IExternalKernelProcessMessageChannel>(capacity: keys.Keys.Length);

        foreach (string key in keys.Keys)
        {
            dict[key] = provider.GetRequiredKeyedService<IExternalKernelProcessMessageChannel>(key);
        }

        return dict;
    }
}
