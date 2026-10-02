// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Licensing;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;

namespace Duende.Spaces.Internal;

/// <summary>
/// Implementation of <see cref="IPartitionedStorageFactory"/> that routes storage operations to the
/// correct pool based on the current space context.
/// </summary>
internal sealed class SpacesPartitionedStorageFactory(
    DefaultPartitionedStorageFactory storageFactory,
    ISpaceContextAccessor spaceContextAccessor,
    SpaceRouting spaceRouting,
    SpacesLicenseValidator licenseValidator) : IPartitionedStorageFactory
{
    /// <summary>
    /// Resolves the <see cref="IPartitionedStorage"/> for the current space context.
    /// </summary>
    /// <param name="dataCategory">The storage category identifying which storage instance to use.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current, non-default space is not available for data access.
    /// </exception>
    public async Task<IPartitionedStorage> GetPartitionedStorageAsync(DataCategoryName dataCategory, Ct ct)
    {
        if (!licenseValidator.ValidateSpaces())
        {
            SpacesLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Spaces feature.");
        }

        var currentSpaceId = spaceContextAccessor.GetSpaceIdOrDefault();

        if (currentSpaceId == SpaceId.Default)
        {
            return storageFactory.GetPartitionedStorage(dataCategory, PoolId.Default);
        }

        // The management space is a well-known, non-persisted space (there is no Space row for
        // it), mirroring PoolId.Management on the storage side. It must be routed directly to
        // pool -1 rather than falling into the persisted-space lookup below, which would always
        // fail to find a matching Space and throw a misleading "not available for data access"
        // error even though the management pool is always available.
        if (currentSpaceId == SpaceId.Management)
        {
            return storageFactory.GetPartitionedStorage(dataCategory, PoolId.Management);
        }

        // Unavailable spaces are deliberately collapsed into a single fail-fast error here,
        // since callers cannot access their data regardless of the underlying reason.
        // This check re-resolves the space's pool via the (potentially cached) routing lookup, so
        // it locks out a deleted space only within cache-freshness bounds, not instantaneously
        // across all instances.
        var poolId = await spaceRouting.TryGetPoolIdAsync(currentSpaceId, ct) ?? throw new InvalidOperationException(
            $"Space '{currentSpaceId}' is not available for data access.");
        return storageFactory.GetPartitionedStorage(dataCategory, poolId);
    }
}
