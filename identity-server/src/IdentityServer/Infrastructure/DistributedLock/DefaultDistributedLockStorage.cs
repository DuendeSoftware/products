// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// Default <see cref="IDistributedLockStorage"/>, resolving <see cref="ManagementStorageAccessor.GetManagementStorage"/>
/// freshly for every operation (matching <see cref="StorageDistributedLock"/>'s "call
/// <c>storage.GetManagementStorage()</c> per operation" contract).
/// </summary>
internal sealed class DefaultDistributedLockStorage(ManagementStorageAccessor accessor) : IDistributedLockStorage
{
    public Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct) =>
        accessor.GetManagementStorage().TryReadAsync(DistributedLockDso.EntityType, key, ct);

    public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) =>
        accessor.GetManagementStorage().CreateAsync(id, dso, [key], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);

    public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) =>
        accessor.GetManagementStorage().UpdateAsync(id, dso, expectedVersion, [key], SearchFieldCollection.Empty, null, [], ct);
}
