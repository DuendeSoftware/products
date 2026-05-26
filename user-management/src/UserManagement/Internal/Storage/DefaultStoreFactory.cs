// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Internal.Storage;

/// <summary>
/// Default implementation of <see cref="IStoreFactory"/> that opens pool 0 from an <see cref="IPooledStore"/>.
/// </summary>
internal sealed class DefaultStoreFactory(IPooledStore pooledStore) : IStoreFactory
{
    private static readonly PoolId DefaultPoolId = 0;

    public IStore GetStore() => pooledStore.OpenPool(DefaultPoolId);
}
