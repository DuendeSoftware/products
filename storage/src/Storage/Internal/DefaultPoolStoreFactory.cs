// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Default implementation of the store factory that will resolve the store from the DI container.
/// It will ALWAYS return a store for the default pool id. 
/// </summary>
/// <param name="pooledStore"></param>
internal class DefaultPoolStoreFactory(IPooledStore pooledStore) : IStoreFactory
{
    public Task<IStore> GetStore(CancellationToken ct) => Task.FromResult(pooledStore.OpenPool(PoolId.Default));
}
