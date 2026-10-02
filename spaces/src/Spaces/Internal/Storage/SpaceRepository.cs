// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.Spaces.Internal.Storage;

internal sealed class SpaceRepository(
    ManagementStorageAccessor storageAccessor,
    DefaultPartitionedStorageFactory storageFactory,
    HybridCache cache,
    ISchemaStore schemaStore)
{
    private const int MaxPoolIdRetries = 3;

    private static readonly NumberField PoolIdField = new("poolId");
    private static readonly BooleanField IsDeletedField = new("isDeleted");

    internal async Task<SaveResult<SpaceId>> CreateAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        PoolId? poolId,
        AttributeValueCollection extendedProperties,
        CancellationToken ct)
    {
        if (poolId is not null)
        {
            return await CreateWithPoolIdAsync(name, patterns, poolId.Value, extendedProperties, ct);
        }

        return await CreateWithAutoPoolIdAsync(name, patterns, extendedProperties, ct);
    }

    private async Task<SaveResult<SpaceId>> CreateWithAutoPoolIdAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        AttributeValueCollection extendedProperties,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxPoolIdRetries; attempt++)
        {
            var maxPoolId = await GetMaxPoolIdAsync(ct);
            var nextPoolId = maxPoolId + 1;

            var result = await CreateWithPoolIdAsync(name, patterns, nextPoolId, extendedProperties, ct);
            if (result.IsSuccess)
            {
                return result;
            }

            // Only retry if the conflict was on the pool ID (race condition).
            // Pattern conflicts are not retryable.
            if (!await IsPoolIdInUseAsync(nextPoolId, ct))
            {
                return result;
            }
        }

        return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed("Failed to allocate pool ID after multiple attempts."));
    }

    private async Task<SaveResult<SpaceId>> CreateWithPoolIdAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        AttributeValueCollection extendedProperties,
        CancellationToken ct)
    {
        var spaceGuid = Guid.CreateVersion7();
        SpaceId spaceId = spaceGuid;

        var dso = ToDso(spaceId, name, enabled: true, poolId, patterns, extendedProperties: extendedProperties);
        var schema = await GetSpaceSchemaAsync(ct);
        var keys = BuildKeys(patterns, poolId, schema, extendedProperties);

        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.CreateAsync(
            UuidV7.From(spaceGuid),
            dso,
            keys,
            BuildSearchFields(poolId, isDeleted: false, schema, extendedProperties),
            Expiration.NoExpiration,
            [],
            ct);

        if (result == CreateResult.Success)
        {
            await BustCacheForSpaceAsync(spaceId, patterns, poolId, ct);
            return SaveResult.Success(spaceId, 1);
        }

        if (result == CreateResult.KeyConflict)
        {
            // Disambiguate: was the conflict on the pool ID or a match pattern?
            if (await IsPoolIdInUseAsync(poolId, ct))
            {
                return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed($"Pool ID {poolId} is already in use.", "PoolId"));
            }

            return SaveResult.Failure<SpaceId>(StorageError.AlreadyExists("match pattern", "unknown", "MatchPatterns"));
        }

        return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed($"Unexpected store result: {result}"));
    }

    private async Task<bool> IsPoolIdInUseAsync(int poolId, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpacePoolDskV1.Create(poolId);
        var lookup = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        return lookup.Found;
    }

    internal async Task<GetResult<SpaceConfiguration>> GetByIdAsync(SpaceId id, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!result.Found)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        var dso = (SpaceDso.V1)result.Dso;
        if (dso.IsDeleted)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        return GetResult.Found(await MapToConfigurationAsync(dso, ct), result.Version.Value);
    }

    /// <summary>
    /// Admin read model. Unlike <see cref="GetByIdAsync"/>, this intentionally includes
    /// logically deleted spaces, allowing administrative tooling to inspect them.
    /// </summary>
    internal async Task<GetResult<SpaceConfiguration>> GetByIdRawAsync(SpaceId id, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!result.Found)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        var dso = (SpaceDso.V1)result.Dso;
        return GetResult.Found(await MapToConfigurationAsync(dso, ct), result.Version.Value);
    }

    internal async Task<SpaceConfiguration?> TryGetByPatternAsync(SpaceMatchPattern matchingCriteria, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpaceMatchPatternDskV1.Create(matchingCriteria.Origin, matchingCriteria.Path);
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!result.Found)
        {
            return null;
        }

        var dso = (SpaceDso.V1)result.Dso;
        if (dso.IsDeleted)
        {
            return null;
        }

        return await MapToConfigurationAsync(dso, ct);
    }

    internal async Task<SpaceConfiguration?> GetByPoolIdAsync(PoolId poolId, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpacePoolDskV1.Create(poolId.Value);
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!result.Found)
        {
            return null;
        }

        var dso = (SpaceDso.V1)result.Dso;
        if (dso.IsDeleted)
        {
            return null;
        }

        return await MapToConfigurationAsync(dso, ct);
    }

    // This method gates runtime routing: it answers "is this origin currently routable?"
    // Deleted spaces are intentionally excluded here because a deleted space is not routable,
    // so its origin must report as unclaimed for path-only routing purposes. This is deliberately
    // asymmetric with IsPatternRegisteredAsync below, which gates admin pattern uniqueness/reservation
    // and does not exclude deleted spaces (a deleted space's pattern stays reserved until purge).
    // This asymmetry is intentional and is not a bug; do not "fix" it by aligning the two filters.
    internal async Task<bool> IsOriginClaimedAsync(string origin, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.QueryAsync<SpaceDso.V1>(
            SpaceDso.EntityType,
            AllExpression.Instance,
            SortParameter.Empty,
            DataRange.FromOffset(null, null),
            ct);

        return result.Items.Any(e =>
            !e.Value.IsDeleted &&
            e.Value.MatchPatterns.Any(p =>
                string.Equals(p.Origin, origin, StringComparison.OrdinalIgnoreCase)));
    }

    internal async Task<SaveResult<SpaceId>> UpdateAsync(
        SpaceConfiguration space,
        AttributeValueCollection extendedProperties,
        int expectedVersion,
        CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();

        var current = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(space.Id), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", space.Id.ToString()));
        }

        if (current.Version.Value != expectedVersion)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        // IsDeleted is derived solely from the row read here, immediately before persistence, and
        // never from a caller-supplied flag. SpaceAdmin.UpdateAsync performs its own best-effort
        // IsDeleted check earlier for fast-fail UX, but that read can go stale if the space is
        // concurrently deleted between SpaceAdmin's read and this one. This read (guarded by the
        // expected-version check above) is the sole authority: it gates both the pattern freeze
        // below and the EAV key/search-field suppression, so a concurrent delete can never be
        // bypassed by a stale "not deleted" caller state.
        var currentDso = (SpaceDso.V1)current.Dso;

        if (currentDso.IsDeleted)
        {
            var currentPatterns = currentDso.MatchPatterns
                .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
                .ToList();

            // A deleted space's match patterns remain reserved until purge (see
            // IsPatternRegisteredAsync). This is the authoritative enforcement of that freeze;
            // SpaceAdmin's earlier check is only a best-effort pre-check and cannot be relied upon
            // under concurrent delete.
            if (!SpaceMatchPatternPolicy.PatternsMatch(space.MatchPatterns, currentPatterns))
            {
                return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed(
                    "Cannot change match patterns of a deleted space. Patterns remain reserved until the space is purged.",
                    "MatchPatterns"));
            }
        }

        var dso = ToDso(space.Id, space.Name, space.Enabled, space.PoolId.Value, space.MatchPatterns, currentDso.IsDeleted, extendedProperties);

        // A logically deleted space keeps its EAV unique keys and search fields dropped (see
        // DeleteAsync), so its attribute values stay reusable by other spaces even if the caller
        // updates ExtendedProperties as part of a non-operational metadata edit. Rebuilding those
        // keys here would silently re-claim values that DeleteAsync intentionally released.
        var schema = currentDso.IsDeleted ? null : await GetSpaceSchemaAsync(ct);
        var eavAttributes = currentDso.IsDeleted ? null : extendedProperties;
        var keys = BuildKeys(space.MatchPatterns, space.PoolId.Value, schema, eavAttributes);

        var result = await partitionedStorage.UpdateAsync(
            UuidV7.From(space.Id),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(space.PoolId.Value, currentDso.IsDeleted, schema, eavAttributes),
            expiration: null,
            [],
            ct);

        if (result == UpdateResult.KeyConflict)
        {
            foreach (var pattern in space.MatchPatterns)
            {
                if (await IsPatternRegisteredAsync(pattern, excludeSpaceId: space.Id, ct))
                {
                    return SaveResult.Failure<SpaceId>(StorageError.AlreadyExists(
                        "match pattern", $"Origin='{pattern.Origin}', Path='{pattern.Path}'", "MatchPatterns"));
                }
            }

            return SaveResult.Failure<SpaceId>(StorageError.AlreadyExists(
                "attribute value", "unknown", "ExtendedProperties"));
        }

        if (result != UpdateResult.Success)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        // Bust cache for both old patterns (in case they were removed) and new patterns
        var oldPatterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        var allPatterns = oldPatterns.Concat(space.MatchPatterns).Distinct().ToList();
        await BustCacheForSpaceAsync(space.Id, allPatterns, currentDso.PoolId, ct, additionalPoolId: space.PoolId.Value);

        return SaveResult.Success<SpaceId>(space.Id, current.Version.Value + 1);
    }

    internal async Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();

        var current = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
        }

        var currentDso = (SpaceDso.V1)current.Dso;
        var deletedPatterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();

        if (currentDso.IsDeleted)
        {
            await BustCacheForSpaceAsync(id, deletedPatterns, currentDso.PoolId, ct);
            return SaveResult.Success(id, current.Version.Value);
        }

        var dso = currentDso with { IsDeleted = true };
        // On delete, drop EAV unique keys so attribute values can be reused by other spaces.
        var keys = BuildKeys(deletedPatterns, currentDso.PoolId, schema: null, attributes: null);

        var result = await partitionedStorage.UpdateAsync(
            UuidV7.From(id.Value),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(currentDso.PoolId, isDeleted: true, schema: null, attributes: null),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        await BustCacheForSpaceAsync(id, deletedPatterns, currentDso.PoolId, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    internal async Task<SaveResult<SpaceId>> UndeleteAsync(SpaceId id, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();

        var current = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
        }

        var currentDso = (SpaceDso.V1)current.Dso;
        var patterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();

        if (!currentDso.IsDeleted)
        {
            // Retry-safe: a previous undelete may have committed the storage flip
            // but crashed before busting caches. Busting here evicts any poisoned
            // negative entries left behind by that window.
            await BustCacheForSpaceAsync(id, patterns, currentDso.PoolId, ct);
            return SaveResult.Success(id, current.Version.Value);
        }

        var schema = await GetSpaceSchemaAsync(ct);
        var extendedProperties = EavMapper.ToMutableCollection(
            EavMapper.ToAttributeValues(currentDso.ExtendedAttributeValues ?? [], schema).ToList());

        var driftErrors = DetectSchemaDrift(currentDso, extendedProperties);
        if (driftErrors.Count > 0)
        {
            return SaveResult.Failure<SpaceId>([.. driftErrors]);
        }

        var conflicts = await DetectEavConflictsAsync(partitionedStorage, extendedProperties, schema, id, ct);
        if (conflicts.Count > 0)
        {
            return SaveResult.Failure<SpaceId>([.. conflicts]);
        }

        var dso = ToDso(id, currentDso.Name, currentDso.Enabled, currentDso.PoolId, patterns, isDeleted: false, extendedProperties);
        var keys = BuildKeys(patterns, currentDso.PoolId, schema, extendedProperties);

        var result = await partitionedStorage.UpdateAsync(
            UuidV7.From(id.Value),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(currentDso.PoolId, isDeleted: false, schema, extendedProperties),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            switch (result)
            {
                case UpdateResult.KeyConflict:
                    var reprobeConflicts = await DetectEavConflictsAsync(partitionedStorage, extendedProperties, schema, id, ct);
                    return reprobeConflicts.Count > 0
                        ? SaveResult.Failure<SpaceId>([.. reprobeConflicts])
                        : SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
                case UpdateResult.DoesNotExist:
                    return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
                case UpdateResult.UnexpectedVersion:
                default:
                    return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
            }
        }

        await BustCacheForSpaceAsync(id, patterns, currentDso.PoolId, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    /// <summary>
    /// Detects extended attribute values that are stored on the space but not representable under the
    /// current schema. EavMapper.ToAttributeValues silently drops such attributes, which would otherwise
    /// cause silent data loss and a hole in uniqueness enforcement when the space is restored.
    /// </summary>
    private static List<StorageError> DetectSchemaDrift(
        SpaceDso.V1 currentDso,
        AttributeValueCollection extendedProperties)
    {
        var storedValues = currentDso.ExtendedAttributeValues ?? [];

        // Safe fast-path: EavMapper.ToAttributeValues only yields codes drawn from the
        // input DSOs, so mapped ⊆ stored. Equal counts therefore imply no drops.
        if (storedValues.Count == extendedProperties.Count)
        {
            return [];
        }

        var mappedCodes = extendedProperties.Select(a => a.Code.Value).ToHashSet(StringComparer.Ordinal);
        var droppedCodes = storedValues
            .Select(v => v.Name)
            .Where(name => !mappedCodes.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return droppedCodes
            .Select(droppedCode => new StorageError(
                "schema_drift",
                $"Attribute '{droppedCode}' is stored on the space but could not be restored under the current schema.",
                [droppedCode]))
            .ToList();
    }

    /// <summary>
    /// Probes management storage for every unique EAV key the restored space would reclaim, so that
    /// an undelete which would collide with another space's uniquely-claimed attribute value can be
    /// rejected up front with a detailed, enumerated error instead of surfacing as a generic
    /// version_conflict from the underlying UpdateAsync call.
    /// </summary>
    private static async Task<List<StorageError>> DetectEavConflictsAsync(
        IPartitionedStorage partitionedStorage,
        AttributeValueCollection extendedProperties,
        IReadOnlyAttributeSchema? schema,
        SpaceId id,
        Ct ct)
    {
        var conflicts = new List<(string AttributeCode, string Value, string HolderSpaceId)>();

        if (schema is null)
        {
            return [];
        }

        foreach (var attribute in extendedProperties)
        {
            if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition) || !definition.IsUnique)
            {
                continue;
            }

            var dsk = AttributeValueDskV1.Create(attribute);
            var lookup = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
            if (!lookup.Found)
            {
                continue;
            }

            var holderDso = (SpaceDso.V1)lookup.Dso;
            if (holderDso.SpaceId == id.Value)
            {
                continue;
            }

            conflicts.Add((attribute.Code.Value, dsk.Value, holderDso.SpaceId.ToString()));
        }

        return conflicts
            .OrderBy(c => c.AttributeCode, StringComparer.Ordinal)
            .Select(c => new StorageError(
                "eav_conflict",
                $"Attribute '{c.AttributeCode}' with value '{c.Value}' is already claimed by space '{c.HolderSpaceId}'.",
                [c.AttributeCode]))
            .ToList();
    }

    internal async Task<SaveResult<SpaceId>> PurgeAsync(SpaceId id, CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();

        var current = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
        }

        var currentDso = (SpaceDso.V1)current.Dso;
        if (!currentDso.IsDeleted)
        {
            return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed(
                "Space must be logically deleted before it can be purged. Call DeleteAsync first."));
        }

        // Purge all data in the space's storage pool
        var poolStore = storageFactory.GetPartitionedStorage(DataCategoryName.Spaces, currentDso.PoolId);
        _ = await poolStore.PurgePoolAsync(ct);

        // Physically remove the space record.
        // The DSO body retains match patterns after soft delete even though secondary keys were cleared.
        var deleteResult = await partitionedStorage.DeleteAsync(SpaceDso.EntityType, UuidV7.From(id.Value), [], ct);
        if (deleteResult == DeleteResult.ConcurrencyConflict)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        // Bust caches for patterns that were in the space
        var patterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        await BustCacheForSpaceAsync(id, patterns, currentDso.PoolId, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    internal async Task<QueryResult<SpaceListItem>> QueryAsync(
        QueryRequest<SpaceFilter, SpaceSortField> request,
        CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.QueryAsync<SpaceDso.V1>(
            SpaceDso.EntityType,
            AllExpression.Instance,
            SortParameter.Empty,
            DataRange.FromOffset(null, null),
            ct);

        var items = result.Items
            .Select(e => e.Value);

        // Apply filter
        if (request.Filter?.FilterValue is { } filter)
        {
            if (filter.Name is { } nameFilter)
            {
                items = items.Where(d => d.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (filter.Enabled is { } enabledFilter)
            {
                items = items.Where(d => d.Enabled == enabledFilter);
            }

            if (filter.IsDeleted is { } isDeletedFilter)
            {
                items = items.Where(d => d.IsDeleted == isDeletedFilter);
            }
        }

        var listItems = items.Select(d => new SpaceListItem
        {
            Id = d.SpaceId,
            Name = d.Name,
            Enabled = d.Enabled,
            PoolId = d.PoolId,
            MatchPatternCount = d.MatchPatterns.Count,
            IsDeleted = d.IsDeleted
        }).ToList();

        return new QueryResult<SpaceListItem>
        {
            Items = listItems,
            HasMoreData = false,
            TotalCount = listItems.Count
        };
    }

    // Pool IDs of deleted spaces are deliberately not recycled by auto-assign.
    // Reuse requires explicit pool ID specification via CreateWithPoolIdAsync after purge.
    internal async Task<int> GetMaxPoolIdAsync(CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.QueryAsync<SpaceDso.V1>(
            SpaceDso.EntityType,
            AllExpression.Instance,
            new SortParameter(PoolIdField, SortDirection.Descending),
            DataRange.FromOffset(null, (DataRangeSize)1),
            ct);

        var top = result.Items.Count > 0 ? result.Items[0] : null;
        return top?.Value.PoolId ?? 0;
    }

    /// <summary>
    /// Returns the number of active (non-deleted) spaces. Backed by an indexed
    /// <c>isDeleted</c> search field so the count is answered at the database layer.
    /// Soft-deleted spaces do not consume the count; a subsequent undelete restores it.
    /// </summary>
    internal async Task<long> GetActiveCountAsync(CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        return await partitionedStorage.CountAsync(SpaceDso.EntityType, IsDeletedField.IsFalse(), ct);
    }

    private async Task BustCacheForSpaceAsync(
        SpaceId spaceId,
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        CancellationToken ct,
        int? additionalPoolId = null)
    {
        // Bust pattern-based cache entries
        foreach (var pattern in patterns)
        {
            if (pattern.Origin != null)
            {
                await cache.RemoveAsync(SpaceCacheKeys.ForOriginClaim(pattern.Origin), ct);
            }
            await cache.RemoveAsync(SpaceCacheKeys.ForPattern(pattern.Origin, pattern.Path), ct);
        }

        // Bust by-ID cache entry
        await cache.RemoveAsync(SpaceCacheKeys.ForSpaceId(spaceId), ct);
        await cache.RemoveAsync(SpaceCacheKeys.ForSpaceIdRouting(spaceId), ct);

        // Bust by-PoolId cache entry, so a soft-delete, undelete or purge is never masked by a stale cached hit.
        await cache.RemoveAsync(SpaceCacheKeys.ForPoolId(poolId), ct);

        // If the space's PoolId itself changed (repository-level defense in depth; SpaceAdmin
        // rejects PoolId changes, but the repository must not rely on that guard alone), the NEW
        // PoolId's cache entry must also be busted. Otherwise a stale negative-cached lookup (or a
        // cached hit belonging to whatever space previously held that PoolId) would keep masking
        // the space under its new PoolId indefinitely.
        if (additionalPoolId is { } newPoolId && newPoolId != poolId)
        {
            await cache.RemoveAsync(SpaceCacheKeys.ForPoolId(newPoolId), ct);
        }
    }

    // This method gates admin pattern uniqueness/reservation: it answers "is this pattern already
    // claimed, and therefore unavailable for a new or updated space to register?" Unlike
    // IsOriginClaimedAsync above, deleted spaces are intentionally NOT excluded here, because a
    // deleted space's pattern remains reserved until it is purged; no live space may claim a pattern
    // still held by a soft-deleted space. This asymmetry with IsOriginClaimedAsync is intentional
    // and is not a bug; do not "fix" it by aligning the two filters.
    internal async Task<bool> IsPatternRegisteredAsync(
        SpaceMatchPattern pattern,
        SpaceId? excludeSpaceId,
        CancellationToken ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpaceMatchPatternDskV1.Create(pattern.Origin, pattern.Path);
        var existing = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!existing.Found)
        {
            return false;
        }

        var existingDso = (SpaceDso.V1)existing.Dso;
        if (excludeSpaceId is not null && existingDso.SpaceId == excludeSpaceId.Value)
        {
            return false;
        }

        return true;
    }

    private static SpaceDso.V1 ToDso(
        SpaceId id,
        string name,
        bool enabled,
        int poolId,
        IReadOnlyList<SpaceMatchPattern> patterns,
        bool isDeleted = false,
        AttributeValueCollection? extendedProperties = null) =>
        new(
            SpaceId: id.Value,
            Name: name,
            Enabled: enabled,
            PoolId: poolId,
            MatchPatterns: patterns.Select(p => new SpaceDso.MatchPatternV1(p.Origin, p.Path)).ToList(),
            IsDeleted: isDeleted,
            ExtendedAttributeValues: extendedProperties is { Count: > 0 }
                ? EavMapper.ToDsoList(extendedProperties)
                : null);

    private static SpaceConfiguration ToEntity(SpaceDso.V1 dso, IReadOnlyAttributeSchema? schema) =>
        new()
        {
            Id = dso.SpaceId,
            Name = dso.Name,
            Enabled = dso.Enabled,
            PoolId = dso.PoolId,
            IsDeleted = dso.IsDeleted,
            MatchPatterns = dso.MatchPatterns
                .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
                .ToList(),
            ExtendedProperties = EavMapper.ToAttributeValues(dso.ExtendedAttributeValues ?? [], schema).ToList()
        };

    private async Task<IReadOnlyAttributeSchema?> GetSpaceSchemaAsync(CancellationToken ct) =>
        await schemaStore.GetAsync(SchemaId.Space, ct);

    private async Task<SpaceConfiguration> MapToConfigurationAsync(SpaceDso.V1 dso, CancellationToken ct)
    {
        var schema = await GetSpaceSchemaAsync(ct);
        return ToEntity(dso, schema);
    }

    private static List<DataStorageKey> BuildKeys(
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        IReadOnlyAttributeSchema? schema,
        AttributeValueCollection? attributes)
    {
        var keys = new List<DataStorageKey>(patterns.Count + 1);

        foreach (var pattern in patterns)
        {
            keys.Add(DataStorageKey.Create(SpaceMatchPatternDskV1.Create(pattern.Origin, pattern.Path)));
        }

        keys.Add(DataStorageKey.Create(SpacePoolDskV1.Create(poolId)));

        if (schema is not null && attributes is { Count: > 0 })
        {
            foreach (var attribute in attributes)
            {
                if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition))
                {
                    continue;
                }

                if (!definition.IsUnique)
                {
                    continue;
                }

                keys.Add(DataStorageKey.Create(AttributeValueDskV1.Create(attribute)));
            }
        }

        return keys;
    }

    private static SearchFieldCollection BuildSearchFields(
        int poolId,
        bool isDeleted,
        IReadOnlyAttributeSchema? schema,
        AttributeValueCollection? attributes)
    {
        var builder = new SearchFieldsBuilder()
            .Add("poolId", poolId)
            .Add("isDeleted", isDeleted);

        if (schema is not null && attributes is { Count: > 0 })
        {
            foreach (var attribute in attributes)
            {
                if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition))
                {
                    continue;
                }

                if (!definition.IsQueryable)
                {
                    continue;
                }

                AddSearchFieldForAttribute(builder, attribute);
            }
        }

        return builder.Build();
    }

    private static void AddSearchFieldForAttribute(SearchFieldsBuilder builder, AttributeValue attribute)
    {
        var fieldPath = $"attr:{attribute.Code.Value}";
        switch (attribute)
        {
            case AttributeValue<string> s:
                _ = builder.Add(fieldPath, s.TypedValue);
                break;
            case AttributeValue<int> i:
                _ = builder.Add(fieldPath, i.TypedValue);
                break;
            case AttributeValue<bool> b:
                _ = builder.Add(fieldPath, b.TypedValue);
                break;
            case AttributeValue<decimal> d:
                _ = builder.Add(fieldPath, d.TypedValue);
                break;
            case AttributeValue<DateOnly> dateOnly:
                var asDto = new DateTimeOffset(dateOnly.TypedValue.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                _ = builder.Add(fieldPath, asDto);
                break;
            case AttributeValue<DateTimeOffset> dto:
                _ = builder.Add(fieldPath, dto.TypedValue);
                break;
        }
    }
}
