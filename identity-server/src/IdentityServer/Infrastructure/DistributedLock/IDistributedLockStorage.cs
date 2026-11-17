// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// The narrow slice of <see cref="IPartitionedStorage"/> that <see cref="StorageDistributedLock"/> and
/// <see cref="StorageLockLease"/> need. This indirection exists so integration tests can substitute a
/// decorator (a store that hangs, fails, or counts concurrent calls) without implementing
/// <see cref="IPartitionedStorage"/> directly: that interface declares an <c>internal</c> member
/// (<c>SetPoolId</c>) that only Duende.Storage's own assembly can implement, and IdentityServer
/// consumes Duende.Storage as a NuGet package, not a project reference. <see cref="DefaultDistributedLockStorage"/>
/// is the real implementation, delegating to <see cref="IPartitionedStorage"/>; production code
/// resolves it through DI (<c>StorageDistributedLock</c>'s constructor takes <see cref="IDistributedLockStorage"/>
/// directly), and tests substitute a decorator by registering their own <see cref="IDistributedLockStorage"/>
/// or constructing <c>StorageDistributedLock</c> with one directly.
/// </summary>
internal interface IDistributedLockStorage
{
    Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct);

    Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct);

    Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct);
}
