// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

using StoragePoolId = Duende.Storage.Internal.PoolId;

namespace Duende.MultiSpace.Internal.Storage;

internal sealed class ManagementStoreAccessor(IPooledStore pooledStore)
{
    internal static readonly StoragePoolId ManagementPoolId = -1;

    internal IStore GetManagementStore() => pooledStore.OpenPool(ManagementPoolId);
}
