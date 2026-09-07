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

internal sealed class SpaceRepository
{
    private readonly ManagementStorageAccessor _storageAccessor;
    private readonly IPooledStore _pooledStore;
    private readonly HybridCache? _cache;
    private readonly ISchemaStore? _schemaStore;
    private const int MaxPoolIdRetries = 3;

    private static readonly NumberField PoolIdField = new("poolId");

    internal SpaceRepository(ManagementStorageAccessor storageAccessor, IPooledStore pooledStore) : this(storageAccessor, pooledStore, null, null) { }

    internal SpaceRepository(ManagementStorageAccessor storageAccessor, IPooledStore pooledStore, HybridCache? cache, ISchemaStore? schemaStore)
    {
        _storageAccessor = storageAccessor;
        _pooledStore = pooledStore;
        _cache = cache;
        _schemaStore = schemaStore;
    }

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

        var storage = _storageAccessor.GetManagementStorage();
        var result = await storage.CreateAsync(
            UuidV7.From(spaceGuid),
            dso,
            keys,
            BuildSearchFields(poolId, schema, extendedProperties),
            Expiration.NoExpiration,
            [],
            ct);

        if (result == CreateResult.Success)
        {
            await BustCacheForSpaceAsync(spaceId, patterns, ct);
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
        var storage = _storageAccessor.GetManagementStorage();
        var dsk = SpacePoolDskV1.Create(poolId);
        var lookup = await storage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        return lookup.Found;
    }

    internal async Task<GetResult<SpaceConfiguration>> GetByIdAsync(SpaceId id, CancellationToken ct)
    {
        var storage = _storageAccessor.GetManagementStorage();
        var result = await storage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!result.Found)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        var dso = (SpaceDso.V1)result.Dso;
        if (dso.IsDeleted)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        var schema = await GetSpaceSchemaAsync(ct);
        return GetResult.Found(ToEntity(dso, schema), result.Version.Value);
    }

    internal async Task<SpaceConfiguration?> TryGetByPatternAsync(SpaceMatchPattern matchingCriteria, CancellationToken ct)
    {
        var storage = _storageAccessor.GetManagementStorage();
        var dsk = SpaceMatchPatternDskV1.Create(matchingCriteria.Origin, matchingCriteria.Path);
        var result = await storage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!result.Found)
        {
            return null;
        }

        var dso = (SpaceDso.V1)result.Dso;
        if (dso.IsDeleted)
        {
            return null;
        }

        var schema = await GetSpaceSchemaAsync(ct);
        return ToEntity(dso, schema);
    }

    internal async Task<bool> IsOriginClaimedAsync(string origin, CancellationToken ct)
    {
        var storage = _storageAccessor.GetManagementStorage();
        var result = await storage.QueryAsync<SpaceDso.V1>(
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
        var storage = _storageAccessor.GetManagementStorage();

        var current = await storage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(space.Id), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", space.Id.ToString()));
        }

        if (current.Version.Value != expectedVersion)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        var currentDso = (SpaceDso.V1)current.Dso;
        var dso = ToDso(space.Id, space.Name, space.Enabled, space.PoolId.Value, space.MatchPatterns, currentDso.IsDeleted, extendedProperties);
        var schema = await GetSpaceSchemaAsync(ct);
        var keys = BuildKeys(space.MatchPatterns, space.PoolId.Value, schema, extendedProperties);

        var result = await storage.UpdateAsync(
            UuidV7.From(space.Id),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(space.PoolId.Value, schema, extendedProperties),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        // Bust cache for both old patterns (in case they were removed) and new patterns
        var oldPatterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        var allPatterns = oldPatterns.Concat(space.MatchPatterns).Distinct().ToList();
        await BustCacheForSpaceAsync(space.Id, allPatterns, ct);

        return SaveResult.Success<SpaceId>(space.Id, current.Version.Value + 1);
    }

    internal async Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, CancellationToken ct)
    {
        var storage = _storageAccessor.GetManagementStorage();

        var current = await storage.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
        }

        var currentDso = (SpaceDso.V1)current.Dso;

        var dso = currentDso with { IsDeleted = true };
        var deletedPatterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        // On delete, drop EAV unique keys so attribute values can be reused by other spaces.
        var keys = BuildKeys(deletedPatterns, currentDso.PoolId, schema: null, attributes: null);

        var result = await storage.UpdateAsync(
            UuidV7.From(id.Value),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(currentDso.PoolId, schema: null, attributes: null),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        await BustCacheForSpaceAsync(id, deletedPatterns, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    internal async Task<SaveResult<SpaceId>> PurgeAsync(SpaceId id, CancellationToken ct)
    {
        var store = _storageAccessor.GetManagementStorage();

        var current = await store.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
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
        var poolStore = _pooledStore.OpenPool(currentDso.PoolId);
        _ = await poolStore.PurgePoolAsync(ct);

        // Physically remove the space record.
        // The DSO body retains match patterns after soft delete even though secondary keys were cleared.
        var deleteResult = await store.DeleteAsync(SpaceDso.EntityType, UuidV7.From(id.Value), [], ct);
        if (deleteResult == DeleteResult.ConcurrencyConflict)
        {
            return SaveResult.Failure<SpaceId>(StorageError.VersionConflict());
        }

        // Bust caches for patterns that were in the space
        var patterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        await BustCacheForSpaceAsync(id, patterns, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    internal async Task<QueryResult<SpaceListItem>> QueryAsync(
        QueryRequest<SpaceFilter, SpaceSortField> request,
        CancellationToken ct)
    {
        var storage = _storageAccessor.GetManagementStorage();
        var result = await storage.QueryAsync<SpaceDso.V1>(
            SpaceDso.EntityType,
            AllExpression.Instance,
            SortParameter.Empty,
            DataRange.FromOffset(null, null),
            ct);

        var items = result.Items
            .Select(e => e.Value)
            .Where(d => !d.IsDeleted);

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
        }

        var listItems = items.Select(d => new SpaceListItem
        {
            Id = d.SpaceId,
            Name = d.Name,
            Enabled = d.Enabled,
            PoolId = d.PoolId,
            MatchPatternCount = d.MatchPatterns.Count
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
        var storage = _storageAccessor.GetManagementStorage();
        var result = await storage.QueryAsync<SpaceDso.V1>(
            SpaceDso.EntityType,
            AllExpression.Instance,
            new SortParameter(PoolIdField, SortDirection.Descending),
            DataRange.FromOffset(null, (DataRangeSize)1),
            ct);

        var top = result.Items.Count > 0 ? result.Items[0] : null;
        return top?.Value.PoolId ?? 0;
    }

    private async Task BustCacheForSpaceAsync(
        SpaceId spaceId,
        IReadOnlyList<SpaceMatchPattern> patterns,
        CancellationToken ct)
    {
        if (_cache == null)
        {
            return;
        }

        // Bust pattern-based cache entries
        foreach (var pattern in patterns)
        {
            if (pattern.Origin != null)
            {
                await _cache.RemoveAsync(SpaceCacheKeys.ForOriginClaim(pattern.Origin), ct);
            }
            await _cache.RemoveAsync(SpaceCacheKeys.ForPattern(pattern.Origin, pattern.Path), ct);
        }

        // Bust by-ID cache entry
        await _cache.RemoveAsync(SpaceCacheKeys.ForSpaceId(spaceId), ct);
    }

    internal async Task<bool> IsPatternRegisteredAsync(
        SpaceMatchPattern pattern,
        SpaceId? excludeSpaceId,
        CancellationToken ct)
    {
        var storage = _storageAccessor.GetManagementStorage();
        var dsk = SpaceMatchPatternDskV1.Create(pattern.Origin, pattern.Path);
        var existing = await storage.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
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
        _schemaStore is not null
            ? await _schemaStore.GetAsync(SchemaId.Space, ct)
            : null;

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
        IReadOnlyAttributeSchema? schema,
        AttributeValueCollection? attributes)
    {
        var builder = new SearchFieldsBuilder()
            .Add("poolId", poolId);

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

