// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal sealed class DefaultStorageFactory(IPooledStore pooledStore, IPoolContextAccessor poolContextAccessor)
    : IStorageFactory
{
    public Task<IStorage> GetStorage(CancellationToken _) =>
        Task.FromResult(pooledStore.OpenPool(poolContextAccessor.CurrentPoolId ?? PoolId.Default));
}
