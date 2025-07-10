// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Process;

internal class LocalUserStateStore
{
    private readonly Dictionary<string, object> _userState = [];

    /// <summary>
    /// Gets the user state of the process.
    /// </summary>
    /// <param name="key">The key to identify the user state.</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Task<T> GetUserStateAsync<T>(string key) where T : class
    {
        if (this._userState.TryGetValue(key, out var value) && value is T typedValue)
        {
            return Task.FromResult(typedValue);
        }

        return Task.FromResult<T>(null!); // HACK
    }

    /// <summary>
    /// Sets the user state of the process.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public Task SetUserStateAsync<T>(string key, T state) where T : class
    {
        this._userState[key] = state ?? throw new ArgumentNullException(nameof(state), "State cannot be null.");
        return Task.CompletedTask;
    }
}
