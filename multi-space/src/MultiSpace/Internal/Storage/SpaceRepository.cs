// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.MultiSpace.Internal.Storage;

internal sealed class SpaceRepository
{
    private readonly ManagementStoreAccessor _storeAccessor;
    private readonly HybridCache? _cache;
    private const int MaxPoolIdRetries = 3;

    private static readonly NumberField PoolIdField = new("poolId");

    internal SpaceRepository(ManagementStoreAccessor storeAccessor) : this(storeAccessor, null) { }

    internal SpaceRepository(ManagementStoreAccessor storeAccessor, HybridCache? cache)
    {
        _storeAccessor = storeAccessor;
        _cache = cache;
    }

    internal async Task<SaveResult<SpaceId>> CreateAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        PoolId? poolId,
        CancellationToken ct)
    {
        if (poolId is not null)
        {
            return await CreateWithPoolIdAsync(name, patterns, poolId.Value, ct);
        }

        return await CreateWithAutoPoolIdAsync(name, patterns, ct);
    }

    private async Task<SaveResult<SpaceId>> CreateWithAutoPoolIdAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxPoolIdRetries; attempt++)
        {
            var maxPoolId = await GetMaxPoolIdAsync(ct);
            var nextPoolId = maxPoolId + 1;

            var result = await CreateWithPoolIdAsync(name, patterns, nextPoolId, ct);
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

        return AdminError.ValidationFailed("Failed to allocate pool ID after multiple attempts.");
    }

    private async Task<SaveResult<SpaceId>> CreateWithPoolIdAsync(
        string name,
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId,
        CancellationToken ct)
    {
        var spaceGuid = Guid.CreateVersion7();
        SpaceId spaceId = spaceGuid;

        var dso = ToDso(spaceId, name, enabled: true, poolId, patterns);
        var keys = BuildKeys(patterns, poolId);

        var store = _storeAccessor.GetManagementStore();
        var result = await store.CreateAsync(
            UuidV7.From(spaceGuid),
            dso,
            keys,
            BuildSearchFields(poolId),
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
                return AdminError.ValidationFailed($"Pool ID {poolId} is already in use.", "PoolId");
            }

            return AdminError.AlreadyExists("match pattern", "unknown", "MatchPatterns");
        }

        return AdminError.ValidationFailed($"Unexpected store result: {result}");
    }

    private async Task<bool> IsPoolIdInUseAsync(int poolId, CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();
        var dsk = SpacePoolDskV1.Create(poolId);
        var lookup = await store.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        return lookup.Found;
    }

    internal async Task<GetResult<SpaceConfiguration>> GetByIdAsync(SpaceId id, CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();
        var result = await store.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!result.Found)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        var dso = (SpaceDso.V1)result.Dso;
        if (dso.IsDeleted)
        {
            return GetResult.NotFound<SpaceConfiguration>();
        }

        return GetResult.Found(ToEntity(dso), result.Version.Value);
    }

    internal async Task<SpaceConfiguration?> TryGetByPatternAsync(SpaceMatchPattern matchingCriteria, CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();
        var dsk = SpaceMatchPatternDskV1.Create(matchingCriteria.Origin, matchingCriteria.Path);
        var result = await store.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
        if (!result.Found)
        {
            return null;
        }

        var dso = (SpaceDso.V1)result.Dso;
        return dso.IsDeleted ? null : ToEntity(dso);
    }

    internal async Task<bool> IsOriginClaimedAsync(string origin, CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();
        var result = await store.QueryAsync<SpaceDso.V1>(
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
        int expectedVersion,
        CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();

        var current = await store.TryReadAsync(SpaceDso.EntityType, UuidV7.From(space.Id), ct);
        if (!current.Found)
        {
            return AdminError.NotFound("space", space.Id.ToString());
        }

        if (current.Version.Value != expectedVersion)
        {
            return AdminError.VersionConflict();
        }

        var currentDso = (SpaceDso.V1)current.Dso;
        var dso = ToDso(space.Id, space.Name, space.Enabled, space.PoolId.Value, space.MatchPatterns, currentDso.IsDeleted);
        var keys = BuildKeys(space.MatchPatterns, space.PoolId.Value);

        var result = await store.UpdateAsync(
            UuidV7.From(space.Id),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(space.PoolId.Value),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            return AdminError.VersionConflict();
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
        var store = _storeAccessor.GetManagementStore();

        var current = await store.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return AdminError.NotFound("space", id.Value.ToString());
        }

        var currentDso = (SpaceDso.V1)current.Dso;

        var dso = currentDso with { IsDeleted = true };
        var keys = BuildKeys(currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList(), currentDso.PoolId);

        var result = await store.UpdateAsync(
            UuidV7.From(id.Value),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(currentDso.PoolId),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            return AdminError.VersionConflict();
        }

        var patterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();
        await BustCacheForSpaceAsync(id, patterns, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    internal async Task<SaveResult<SpaceId>> ChangePoolIdAsync(SpaceId id, int newPoolId, CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();

        var current = await store.TryReadAsync(SpaceDso.EntityType, UuidV7.From(id.Value), ct);
        if (!current.Found)
        {
            return AdminError.NotFound("space", id.Value.ToString());
        }

        var currentDso = (SpaceDso.V1)current.Dso;
        var patterns = currentDso.MatchPatterns
            .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
            .ToList();

        var dso = ToDso(id, currentDso.Name, currentDso.Enabled, newPoolId, patterns, currentDso.IsDeleted);
        var keys = BuildKeys(patterns, newPoolId);

        var result = await store.UpdateAsync(
            UuidV7.From(id.Value),
            dso,
            current.Version.Value,
            keys,
            BuildSearchFields(newPoolId),
            expiration: null,
            [],
            ct);

        if (result != UpdateResult.Success)
        {
            return AdminError.VersionConflict();
        }

        await BustCacheForSpaceAsync(id, patterns, ct);

        return SaveResult.Success(id, current.Version.Value + 1);
    }

    internal async Task<QueryResult<SpaceListItem>> QueryAsync(
        QueryRequest<SpaceFilter, SpaceSortField> request,
        CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();
        var result = await store.QueryAsync<SpaceDso.V1>(
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

    internal async Task<int> GetMaxPoolIdAsync(CancellationToken ct)
    {
        var store = _storeAccessor.GetManagementStore();
        var result = await store.QueryAsync<SpaceDso.V1>(
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
        var store = _storeAccessor.GetManagementStore();
        var dsk = SpaceMatchPatternDskV1.Create(pattern.Origin, pattern.Path);
        var existing = await store.TryReadAsync(SpaceDso.EntityType, DataStorageKey.Create(dsk), ct);
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
        bool isDeleted = false) =>
        new(
            SpaceId: id.Value,
            Name: name,
            Enabled: enabled,
            PoolId: poolId,
            MatchPatterns: patterns.Select(p => new SpaceDso.MatchPatternV1(p.Origin, p.Path)).ToList(),
            IsDeleted: isDeleted);

    private static SpaceConfiguration ToEntity(SpaceDso.V1 dso) =>
        new()
        {
            Id = dso.SpaceId,
            Name = dso.Name,
            Enabled = dso.Enabled,
            PoolId = dso.PoolId,
            IsDeleted = dso.IsDeleted,
            MatchPatterns = dso.MatchPatterns
                .Select(p => new SpaceMatchPattern { Origin = p.Origin, Path = p.Path })
                .ToList()
        };

    private static List<DataStorageKey> BuildKeys(
        IReadOnlyList<SpaceMatchPattern> patterns,
        int poolId)
    {
        var keys = new List<DataStorageKey>(patterns.Count + 1);

        foreach (var pattern in patterns)
        {
            keys.Add(DataStorageKey.Create(SpaceMatchPatternDskV1.Create(pattern.Origin, pattern.Path)));
        }

        keys.Add(DataStorageKey.Create(SpacePoolDskV1.Create(poolId)));

        return keys;
    }

    private static SearchFieldCollection BuildSearchFields(int poolId) =>
        new SearchFieldsBuilder()
            .Add("poolId", poolId)
            .Build();
}
