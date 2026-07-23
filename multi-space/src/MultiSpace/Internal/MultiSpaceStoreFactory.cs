// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

using StoragePoolId = Duende.Storage.Internal.PoolId;

namespace Duende.MultiSpace.Internal;

/// <summary>
/// Implementation of <see cref="IStoreFactory"/> that routes storage operations to the
/// correct pool based on the current space context.
/// </summary>
internal sealed class MultiSpaceStoreFactory(
    IPooledStore pooledStore,
    ISpaceContextAccessor spaceContextAccessor,
    ISpaceStore spaceStore) : IStoreFactory
{

    public async Task<IStore> GetStore(CancellationToken ct)
    {
        var currentSpaceId = spaceContextAccessor.GetSpaceIdOrDefault();

        if (currentSpaceId == SpaceId.Default)
        {
            return pooledStore.OpenPool(StoragePoolId.Default);
        }

        var space = await spaceStore.TryGetSpace(currentSpaceId, ct);
        var poolId = space?.PoolId ?? throw new InvalidOperationException($"Space with id {currentSpaceId} not found.");
        return pooledStore.OpenPool((StoragePoolId)poolId.Value);
    }
}
