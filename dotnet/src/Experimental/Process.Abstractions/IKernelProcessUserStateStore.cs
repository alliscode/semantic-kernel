// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.SemanticKernel;
public interface IKernelProcessUserStateStore
{
    Task<T> GetUserStateAsync<T>(string key) where T : class;
    Task SetUserStateAsync<T>(string key, T state) where T : class;
}
