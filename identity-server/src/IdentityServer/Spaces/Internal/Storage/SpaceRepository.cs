// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Pagination;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Spaces.Internal.Storage;

internal sealed class SpaceRepository(
    ManagementStorageAccessor storageAccessor,
    ICrossPartitionStorageFactory crossPartitionStorageFactory,
    IStorageInstanceRouter storageInstanceRouter,
    [FromKeyedServices(SpacesServiceProviderKeys.SpacesCache)] HybridCache cache,
    ISchemaStore schemaStore,
    ISpaceContextAccessor spaceContextAccessor,
    ILogger<SpaceRepository> logger)
{
    private const int MaxPoolIdRetries = 3;

    private static readonly DataStorageKey PoolIdHighWaterMarkKey =
        DataStorageKey.Create(new SpacePoolIdHighWaterMarkDskV1());

    private static readonly NumberField PoolIdField = new("poolId");
    private static readonly StringField NameField = new("name");
    private static readonly BooleanField EnabledField = new("enabled");
    private static readonly BooleanField IsDeletedField = new("isDeleted");

    // One item per match pattern with a non-null Origin (any path), written with AddExactMatch.
    // StringField.Equals compares the same case-insensitive hash, and isMultiValued matches any item.
    private static readonly StringField OriginField = new("origin", isMultiValued: true);

    // One item per match pattern with a non-null Origin and a non-null Path. Used only by the admin
    // origin/path consistency check (CheckOriginPathConsistencyAsync in SpaceAdmin).
    private static readonly StringField OriginWithPathField = new("originWithPath", isMultiValued: true);

    internal async Task<SaveResult<SpaceId>> CreateAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        PoolId? poolId,
        AttributeValueCollection extendedProperties,
        Ct ct)
    {
        if (poolId is not null)
        {
            return await CreateWithExplicitPoolIdAsync(name, patterns, poolId.Value, extendedProperties, ct);
        }

        return await CreateWithAutoPoolIdAsync(name, patterns, extendedProperties, ct);
    }

    private async Task<SaveResult<SpaceId>> CreateWithExplicitPoolIdAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        AttributeValueCollection extendedProperties,
        Ct ct)
    {
        for (var attempt = 0; attempt < MaxPoolIdRetries; attempt++)
        {
            var highWaterMark = await ReadPoolIdHighWaterMarkAsync(ct);
            var currentMaxPoolId = highWaterMark.Found
                ? SpacePoolIdHighWaterMarkDso.ToCurrent(highWaterMark.Dso).MaxPoolId
                : await GetMaxPoolIdAsync(ct);

            var nextMaxPoolId = Math.Max(currentMaxPoolId, poolId);
            if (highWaterMark.Found && nextMaxPoolId == currentMaxPoolId)
            {
                return await CreateWithPoolIdAsync(name, patterns, poolId, extendedProperties, ct);
            }

            var result = await TryCreateWithPoolIdAndHighWaterMarkAsync(
                name, patterns, poolId, nextMaxPoolId, highWaterMark, extendedProperties, ct);
            if (result is not null)
            {
                return result;
            }
        }

        return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
            "Failed to update the highest assigned pool ID after multiple attempts."));
    }

    private async Task<SaveResult<SpaceId>> CreateWithAutoPoolIdAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        AttributeValueCollection extendedProperties,
        Ct ct)
    {
        for (var attempt = 0; attempt < MaxPoolIdRetries; attempt++)
        {
            var highWaterMark = await ReadPoolIdHighWaterMarkAsync(ct);
            var maxPoolId = highWaterMark.Found
                ? SpacePoolIdHighWaterMarkDso.ToCurrent(highWaterMark.Dso).MaxPoolId
                : await GetMaxPoolIdAsync(ct);

            if (maxPoolId == int.MaxValue)
            {
                return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
                    $"No pool ID is available above {int.MaxValue} for automatic assignment.", "PoolId"));
            }

            var nextPoolId = maxPoolId + 1;

            var result = await TryCreateWithPoolIdAndHighWaterMarkAsync(
                name, patterns, nextPoolId, nextPoolId, highWaterMark, extendedProperties, ct);

            if (result is not null)
            {
                return result;
            }
        }

        return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
            "Failed to allocate a unique pool ID after multiple attempts."));
    }

    private async Task<SaveResult<SpaceId>?> TryCreateWithPoolIdAndHighWaterMarkAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        int nextMaxPoolId,
        StoreGetResult highWaterMark,
        AttributeValueCollection extendedProperties,
        Ct ct)
    {
        var spaceGuid = Guid.CreateVersion7();
        SpaceId spaceId = spaceGuid;
        var spaceDso = ToDso(spaceId, name, enabled: true, poolId, patterns, extendedProperties: extendedProperties);
        var highWaterMarkDso = new SpacePoolIdHighWaterMarkDso.V1(nextMaxPoolId);
        var schema = await GetSpaceSchemaAsync(ct);
        var keys = BuildKeys(patterns, poolId, schema, extendedProperties);
        var partitionedStorage = storageAccessor.GetManagementStorage();

        IStorageOperation highWaterMarkOperation = highWaterMark.Found
            ? UpdateOperation.For(
                SpacePoolIdHighWaterMarkDso.Id,
                highWaterMarkDso,
                highWaterMark.Version.Value,
                [PoolIdHighWaterMarkKey],
                SearchFieldCollection.Empty,
                Expiration.NoExpiration)
            : CreateOperation.For(
                SpacePoolIdHighWaterMarkDso.Id,
                highWaterMarkDso,
                [PoolIdHighWaterMarkKey],
                SearchFieldCollection.Empty,
                Expiration.NoExpiration);

        var spaceOperation = CreateOperation.For(
            UuidV7.From(spaceGuid),
            spaceDso,
            keys,
            BuildSearchFields(name, enabled: true, poolId, isDeleted: false, patterns, schema, extendedProperties),
            Expiration.NoExpiration);

        var batch = await partitionedStorage.ExecuteBatchAsync([highWaterMarkOperation, spaceOperation], [], ct);
        if (batch.Success)
        {
            await BustCacheForSpaceAsync(spaceId, patterns, poolId, ct);
            return SaveResult.Success(spaceId, 1);
        }

        var failedOperation = batch.Results[^1];

        if (batch.Results.Count == 1 &&
            (highWaterMark.Found
                ? failedOperation.Outcome is OperationOutcome.UnexpectedVersion or OperationOutcome.DoesNotExist
                : failedOperation.Outcome == OperationOutcome.AlreadyExists))
        {
            return null;
        }

        if (batch.Results.Count == 1 && failedOperation.Outcome == OperationOutcome.KeyConflict)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
                "The pool ID high-water mark unique key is already associated with another record."));
        }

        if (batch.Results.Count == 2 && failedOperation.Outcome == OperationOutcome.KeyConflict)
        {
            return await CreateKeyConflictResultAsync(poolId, ct);
        }

        return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
            $"Unexpected store result while creating a space: {failedOperation.Outcome}."));
    }

    private async Task<StoreGetResult> ReadPoolIdHighWaterMarkAsync(Ct ct) =>
        await storageAccessor.GetManagementStorage().TryReadAsync(
            SpacePoolIdHighWaterMarkDso.EntityType,
            SpacePoolIdHighWaterMarkDso.Id,
            ct);

    private async Task<SaveResult<SpaceId>> CreateWithPoolIdAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        AttributeValueCollection extendedProperties,
        Ct ct)
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
            BuildSearchFields(name, enabled: true, poolId, isDeleted: false, patterns, schema, extendedProperties),
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
            return await CreateKeyConflictResultAsync(poolId, ct);
        }

        return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed($"Unexpected store result: {result}"));
    }

    private async Task<SaveResult<SpaceId>> CreateKeyConflictResultAsync(int poolId, Ct ct)
    {
        if (await IsPoolIdInUseAsync(poolId, ct))
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
                $"Pool ID {poolId} is already in use.", "PoolId"));
        }

        return SaveResult.Failure<SpaceId>(
            ErrorResult.AlreadyExists("match pattern", "unknown", "MatchPatterns"));
    }

    private async Task<bool> IsPoolIdInUseAsync(int poolId, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpacePoolDskV1.Create(poolId);
        var lookup = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        return lookup.Found;
    }

    /// <summary>
    /// Uncached read of the stored space record (not the mapped <see cref="SpaceConfiguration"/>) by
    /// ID, for <see cref="SpaceStore"/> to cache itself. Excludes logically deleted spaces, but not
    /// disabled ones: <see cref="SpaceStore"/> applies the <c>Enabled</c> filter after reading from
    /// its cache, so toggling a space's enabled flag is picked up as soon as the cache entry (busted
    /// on every write) is refreshed.
    /// </summary>
    internal async Task<SpaceDso.V1?> GetDsoByIdAsync(SpaceId id, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!result.Found)
        {
            return null;
        }

        var dso = SpaceDso.ToCurrent(result.Dso);
        return dso.IsDeleted ? null : dso;
    }

    /// <summary>
    /// Admin read model. Unlike <see cref="GetDsoByIdAsync"/>, this intentionally includes
    /// logically deleted spaces, allowing administrative tooling to inspect them.
    /// </summary>
    internal async Task<GetResult<SpaceConfiguration>> GetByIdRawAsync(SpaceId id, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!result.Found)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        var dso = SpaceDso.ToCurrent(result.Dso);
        return GetResult.Found(await MapToConfigurationAsync(dso, ct), result.Version.Value);
    }

    /// <summary>
    /// Uncached read of the stored space record (not the mapped <see cref="SpaceConfiguration"/>) by
    /// match pattern, for <see cref="SpaceStore"/> to cache itself. Excludes logically deleted
    /// spaces; the <c>Enabled</c> filter is applied by <see cref="SpaceStore"/> after the cache read.
    /// </summary>
    internal async Task<SpaceDso.V1?> TryGetDsoByPatternAsync(SpaceMatchPattern matchingCriteria, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpaceMatchPatternDskV1.Create(matchingCriteria.Origin, matchingCriteria.Path);
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!result.Found)
        {
            return null;
        }

        var dso = SpaceDso.ToCurrent(result.Dso);
        return dso.IsDeleted ? null : dso;
    }

    /// <summary>
    /// Uncached read of the stored space record (not the mapped <see cref="SpaceConfiguration"/>) by
    /// pool ID, for <see cref="SpaceStore"/> to cache itself. Excludes logically deleted spaces; the
    /// <c>Enabled</c> filter is applied by <see cref="SpaceStore"/> after the cache read.
    /// </summary>
    internal async Task<SpaceDso.V1?> GetDsoByPoolIdAsync(PoolId poolId, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpacePoolDskV1.Create(poolId.Value);
        var result = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!result.Found)
        {
            return null;
        }

        var dso = SpaceDso.ToCurrent(result.Dso);
        return dso.IsDeleted ? null : dso;
    }

    // This method gates runtime routing: it answers "is this origin currently routable?"
    // Deleted spaces are intentionally excluded here because a deleted space is not routable,
    // so its origin must report as unclaimed for path-only routing purposes. This is deliberately
    // asymmetric with IsPatternRegisteredAsync below, which gates admin pattern uniqueness/reservation
    // and does not exclude deleted spaces (a deleted space's pattern stays reserved until purge).
    // This asymmetry is intentional and is not a bug; do not "fix" it by aligning the two filters.
    //
    // Backed by the indexed "origin" search field (OriginField) and answered with a single-row
    // range query, so the answer does not depend on how many spaces exist or which page an
    // origin-claimed space happens to fall on.
    internal async Task<bool> IsOriginClaimedAsync(string origin, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.QueryAsync(
            SpaceDso.EntityType,
            new AndExpression([OriginField.Equals(origin), IsDeletedField.IsFalse()]),
            SortParameter.Empty,
            DataRange.FromContinuationToken(ContinuationToken.Beginning, (DataRangeSize)1),
            ct);

        return result.Items.Count > 0;
    }

    // Answers the admin-facing question "does another space already use this origin together with a
    // path?" Used only for the (Origin, null) side of CheckOriginPathConsistencyAsync in SpaceAdmin:
    // the (Origin, Path) side is answered by a direct key lookup via IsPatternRegisteredAsync instead,
    // because (Origin, null) is itself a unique storage key. Deleted spaces are included (not excluded),
    // matching IsPatternRegisteredAsync: a soft-deleted space's origin usage stays reserved until purge.
    // A range of 2 is enough: at most one matching item can belong to excludeSpaceId, so if any other
    // item exists, it belongs to a different space.
    internal async Task<bool> IsOriginUsedWithPathByAnotherSpaceAsync(string origin, SpaceId? excludeSpaceId, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.QueryAsync(
            SpaceDso.EntityType,
            OriginWithPathField.Equals(origin),
            SortParameter.Empty,
            DataRange.FromContinuationToken(ContinuationToken.Beginning, (DataRangeSize)2),
            ct);

        return result.Items.Any(e => excludeSpaceId is null || SpaceDso.ToCurrent(e.Value).SpaceId != excludeSpaceId.Value);
    }

    internal async Task<SaveResult<SpaceId>> UpdateAsync(
        SpaceConfiguration space,
        AttributeValueCollection extendedProperties,
        int expectedVersion,
        Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();

        var current = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(space.Id), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.NotFound("space", space.Id.ToString()));
        }

        if (current.Version.Value != expectedVersion)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.VersionConflict());
        }

        // IsDeleted is derived solely from the row read here, immediately before persistence, and
        // never from a caller-supplied flag. SpaceAdmin.UpdateAsync performs its own best-effort
        // IsDeleted check earlier for fast-fail UX, but that read can go stale if the space is
        // concurrently deleted between SpaceAdmin's read and this one. This read (guarded by the
        // expected-version check above) is the sole authority: it gates both the pattern freeze
        // below and the EAV key/search-field suppression, so a concurrent delete can never be
        // bypassed by a stale "not deleted" caller state.
        var currentDso = SpaceDso.ToCurrent(current.Dso);

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
                return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
                    "Cannot change match patterns of a deleted space. Patterns remain reserved until the space is purged.",
                    "MatchPatterns"));
            }
        }

        var dso = ToDso(space.Id, space.Name, space.Enabled, space.PoolId.Value, space.MatchPatterns, currentDso.IsDeleted, extendedProperties);

        // A logically deleted space keeps its EAV unique keys and attribute search fields dropped
        // (see DeleteAsync), so its attribute values stay reusable by other spaces even if the
        // caller updates ExtendedProperties as part of a non-operational metadata edit. Rebuilding
        // those keys here would silently re-claim values that DeleteAsync intentionally released.
        var schema = currentDso.IsDeleted ? null : await GetSpaceSchemaAsync(ct);
        var eavAttributes = currentDso.IsDeleted ? null : extendedProperties;
        var keys = BuildKeys(space.MatchPatterns, space.PoolId.Value, schema, eavAttributes);

        var result = await partitionedStorage.UpdateAsync(
            UuidV7.From(space.Id),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(space.Name, space.Enabled, space.PoolId.Value, currentDso.IsDeleted, space.MatchPatterns, schema, eavAttributes),
            expiration: null,
            [],
            ct);

        if (result == UpdateResult.KeyConflict)
        {
            foreach (var pattern in space.MatchPatterns)
            {
                if (await IsPatternRegisteredAsync(pattern, excludeSpaceId: space.Id, ct))
                {
                    return SaveResult.Failure<SpaceId>(ErrorResult.AlreadyExists(
                        "match pattern", $"Origin='{pattern.Origin}', Path='{pattern.Path}'", "MatchPatterns"));
                }
            }

            return SaveResult.Failure<SpaceId>(ErrorResult.AlreadyExists(
                "attribute value", "unknown", "ExtendedProperties"));
        }

        if (result != UpdateResult.Success)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.VersionConflict());
        }

        // Bust cache for both old patterns (in case they were removed) and new patterns
        var oldPatterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        var allPatterns = oldPatterns.Concat(space.MatchPatterns).Distinct().ToList();
        await BustCacheForSpaceAsync(space.Id, allPatterns, currentDso.PoolId, ct, additionalPoolId: space.PoolId.Value);

        return SaveResult.Success<SpaceId>(space.Id, current.Version.Value + 1);
    }

    internal async Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();

        var current = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.NotFound("space", id.Value.ToString()));
        }

        var currentDso = SpaceDso.ToCurrent(current.Dso);
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
            BuildSearchFields(currentDso.Name, currentDso.Enabled, currentDso.PoolId, isDeleted: true, deletedPatterns, schema: null, attributes: null),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.VersionConflict());
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
            return SaveResult.Failure<SpaceId>(ErrorResult.NotFound("space", id.Value.ToString()));
        }

        var currentDso = SpaceDso.ToCurrent(current.Dso);
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
            BuildSearchFields(currentDso.Name, currentDso.Enabled, currentDso.PoolId, isDeleted: false, patterns, schema, extendedProperties),
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
                        : SaveResult.Failure<SpaceId>(ErrorResult.VersionConflict());
                case UpdateResult.DoesNotExist:
                    return SaveResult.Failure<SpaceId>(ErrorResult.NotFound("space", id.Value.ToString()));
                case UpdateResult.UnexpectedVersion:
                default:
                    return SaveResult.Failure<SpaceId>(ErrorResult.VersionConflict());
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
    private static List<ErrorResult> DetectSchemaDrift(
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
            .Select(droppedCode => new ErrorResult(
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
    private static async Task<List<ErrorResult>> DetectEavConflictsAsync(
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

            var holderDso = SpaceDso.ToCurrent(lookup.Dso);
            if (holderDso.SpaceId == id.Value)
            {
                continue;
            }

            conflicts.Add((attribute.Code.Value, dsk.Value, holderDso.SpaceId.ToString()));
        }

        return conflicts
            .OrderBy(c => c.AttributeCode, StringComparer.Ordinal)
            .Select(c => new ErrorResult(
                "eav_conflict",
                $"Attribute '{c.AttributeCode}' with value '{c.Value}' is already claimed by space '{c.HolderSpaceId}'.",
                [c.AttributeCode]))
            .ToList();
    }

    internal async Task<SaveResult<SpaceId>> PurgeAsync(SpaceId id, Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();

        var current = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.NotFound("space", id.Value.ToString()));
        }

        var currentDso = SpaceDso.ToCurrent(current.Dso);
        if (!currentDso.IsDeleted)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
                "Space must be logically deleted before it can be purged. Call DeleteAsync first."));
        }

        if (currentDso.PoolId == PoolId.Management.Value || currentDso.PoolId == PoolId.Default.Value)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.ValidationFailed(
                $"Pool ID {currentDso.PoolId} is reserved and can't be purged.", "PoolId"));
        }

        // Products can map their data categories to any storage instance, so the pool may have rows in any of them.
        var failedInstances = await PurgePoolInEveryInstanceAsync(currentDso.PoolId, ct);
        if (failedInstances.Count > 0)
        {
            return SaveResult.Failure<SpaceId>(ErrorResult.PurgeIncomplete(currentDso.PoolId, failedInstances));
        }

        // Physically remove the space record.
        // The DSO body retains match patterns after soft delete even though secondary keys were cleared.
        await partitionedStorage.DeleteAsync(SpaceDso.EntityType, UuidV7.From(id.Value), [], ct);

        // Bust caches for patterns that were in the space
        var patterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        await BustCacheForSpaceAsync(id, patterns, currentDso.PoolId, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    private async Task<IReadOnlyList<StorageInstanceId>> PurgePoolInEveryInstanceAsync(int poolId, Ct ct)
    {
        var failedInstances = new List<StorageInstanceId>();
        foreach (var storageInstanceId in storageInstanceRouter.GetAll())
        {
            try
            {
                var crossPartitionStorage = await crossPartitionStorageFactory.GetCrossPartitionStorageAsync(storageInstanceId, ct);
                _ = await crossPartitionStorage.PurgePoolAsync(poolId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // A failing instance must not stop the purge of the others; any provider exception counts as a failure.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.PurgePoolFailed(ex, poolId, storageInstanceId);
                failedInstances.Add(storageInstanceId);
            }
        }

        return failedInstances;
    }

    internal async Task<QueryResult<SpaceListItem>> QueryAsync(
        QueryRequest<SpaceFilter, SpaceSortField> request,
        Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var filter = request.Filter?.FilterValue;
        var filterExpressions = new List<IQueryFilterExpression>();
        if (filter?.Name is { } nameFilter)
        {
            filterExpressions.Add(NameField.Contains(nameFilter));
        }

        if (filter?.Enabled is { } enabledFilter)
        {
            filterExpressions.Add(EnabledField.Equals(enabledFilter));
        }

        if (filter?.IsDeleted is { } isDeletedFilter)
        {
            filterExpressions.Add(IsDeletedField.Equals(isDeletedFilter));
        }

        var storageFilter = filterExpressions.Count switch
        {
            0 => AllExpression.Instance,
            1 => filterExpressions[0],
            _ => new AndExpression(filterExpressions)
        };

        var result = await partitionedStorage.QueryAsync(
            SpaceDso.EntityType,
            storageFilter,
            BuildSort(request.Sort),
            request.Range ?? DataRange.FromContinuationToken(ContinuationToken.Beginning),
            ct);

        var listItems = result.Items.Select(e => SpaceDso.ToCurrent(e.Value)).Select(d => new SpaceListItem
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
            HasMoreData = result.HasMoreData,
            HasPreviousData = result.HasPreviousData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages
        };
    }

    private static SortParameter BuildSort(SortBy.SortByField<SpaceSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(PoolIdField, StorageSortDirection.Ascending);
        }

        Field field = sort.Field switch
        {
            SpaceSortField.Name => NameField,
            SpaceSortField.Enabled => EnabledField,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, "Unknown sort field")
        };

        var direction = sort.Direction switch
        {
            StorageSortDirection.Ascending => StorageSortDirection.Ascending,
            StorageSortDirection.Descending => StorageSortDirection.Descending,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Direction, "Unknown sort direction")
        };

        return new SortParameter(field, direction);
    }

    // Used only to initialize the high-water mark the first time a space is created.
    // Logically deleted spaces retain their records and therefore contribute to the maximum.
    internal async Task<int> GetMaxPoolIdAsync(Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var result = await partitionedStorage.QueryAsync(
            SpaceDso.EntityType,
            AllExpression.Instance,
            new SortParameter(PoolIdField, SortDirection.Descending),
            DataRange.FromOffset(null, (DataRangeSize)1),
            ct);

        return result.Items.Count > 0 ? SpaceDso.ToCurrent(result.Items[0].Value).PoolId : 0;
    }

    /// <summary>
    /// Returns the number of active (non-deleted) spaces. Backed by an indexed
    /// <c>isDeleted</c> search field so the count is answered at the database layer.
    /// Soft-deleted spaces do not consume the count; a subsequent undelete restores it.
    /// </summary>
    internal async Task<long> GetActiveCountAsync(Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        return await partitionedStorage.CountAsync(SpaceDso.EntityType, IsDeletedField.IsFalse(), ct);
    }

    private async Task BustCacheForSpaceAsync(
        SpaceId spaceId,
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        Ct ct,
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
        Ct ct)
    {
        var partitionedStorage = storageAccessor.GetManagementStorage();
        var dsk = SpaceMatchPatternDskV1.Create(pattern.Origin, pattern.Path);
        var existing = await partitionedStorage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!existing.Found)
        {
            return false;
        }

        var existingDso = SpaceDso.ToCurrent(existing.Dso);
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

    private async Task<IReadOnlyAttributeSchema?> GetSpaceSchemaAsync(Ct ct)
    {
        using (spaceContextAccessor.SetSpace(SpaceId.Management))
        {
            return await schemaStore.GetAsync(SchemaId.Space, ct);
        }
    }

    /// <summary>
    /// Maps the stored space record to <see cref="SpaceConfiguration"/> with the current space schema.
    /// <see cref="SpaceStore"/> calls it with cached <see cref="SpaceDso.V1"/> records too, so a cached
    /// read is typed by the schema of the moment, not by the schema that applied when it was cached.
    /// </summary>
    internal async Task<SpaceConfiguration> MapToConfigurationAsync(SpaceDso.V1 dso, Ct ct)
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
        string name,
        bool enabled,
        int poolId,
        bool isDeleted,
        IReadOnlyList<SpaceMatchPattern> patterns,
        IReadOnlyAttributeSchema? schema,
        AttributeValueCollection? attributes)
    {
        var builder = new SearchFieldsBuilder()
            .Add("name", name)
            .Add("enabled", enabled)
            .Add("poolId", poolId)
            .Add("isDeleted", isDeleted);

        // Index patterns regardless of isDeleted: a soft-deleted space's origin must still answer
        // IsOriginUsedWithPathByAnotherSpaceAsync (admin reservation check), even though it must not
        // answer IsOriginClaimedAsync (runtime routing), which filters isDeleted at query time instead.
        var originItemIndex = 0;
        var originWithPathItemIndex = 0;
        foreach (var pattern in patterns)
        {
            if (pattern.Origin is null)
            {
                continue;
            }

            builder.AddExactMatch("origin", originItemIndex++, pattern.Origin);

            if (pattern.Path is not null)
            {
                builder.AddExactMatch("originWithPath", originWithPathItemIndex++, pattern.Origin);
            }
        }

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
