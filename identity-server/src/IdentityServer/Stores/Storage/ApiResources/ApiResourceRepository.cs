// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;

namespace Duende.IdentityServer.Stores.Storage.ApiResources;

/// <summary>
/// The outcome of a batch operation that creates or updates an API resource together with its scope links.
/// </summary>
internal enum ApiResourceBatchOutcomeKind
{
    Success,
    AlreadyExists,
    KeyConflict,
    UnexpectedVersion,
    ResourceDoesNotExist,
    ScopeDoesNotExist
}

/// <summary>
/// The outcome of a batch operation, naming the scope when <see cref="Kind"/> is
/// <see cref="ApiResourceBatchOutcomeKind.ScopeDoesNotExist"/>.
/// </summary>
internal readonly record struct ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind Kind, string? ScopeName = null)
{
    public static readonly ApiResourceBatchOutcome Success = new(ApiResourceBatchOutcomeKind.Success);
}

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiResourceRepository(IPartitionedStorageFactory partitionedStorageFactory, ApiScopeRepository apiScopeRepository)
{
    private const int LinkPageSize = 200;

    private Task<IPartitionedStorage> GetPartitionedStorage(Ct ct) => partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Configuration, ct);

    internal enum Keys
    {
        Name = 1
    }

    private static class Fields
    {
        public static readonly StringField Name = new("Name");
        public static readonly BooleanField Enabled = new("Enabled");
        public static readonly GuidField ApiResourceId = new("ApiResourceId");
    }

    internal async Task<ApiResourceBatchOutcome> CreateAsync(UuidV7 id, ApiResourceDso.V1 dso, IReadOnlyList<(string Name, Guid Id)> scopes, Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            CreateOperation.For(id, dso, BuildKeys(dso), BuildSearchFields(dso), Expiration.NoExpiration)
        };

        foreach (var scope in scopes)
        {
            operations.Add(LinkOperation.For(ApiResourceLinkDefinitions.ApiResourceScope, id, UuidV7.From(scope.Id)));
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToCreateOutcome(batchResult, scopes);
    }

    internal async Task<(ApiResourceDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(ApiResourceDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? (ApiResourceDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(ApiResourceDso.V1 Dso, int Version)?> TryReadByNameAsync(string name, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(
            ApiResourceDso.EntityType,
            DataStorageKey.Create(ApiResourceNameDskV1.Create(name)),
            ct);
        return result.Found ? (ApiResourceDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 id, ApiResourceDso.V1 dso, int expectedVersion, Ct ct) =>
        await (await GetPartitionedStorage(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            BuildKeys(dso),
            BuildSearchFields(dso),
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

    internal async Task<ApiResourceBatchOutcome> UpdateAsync(
        UuidV7 id,
        ApiResourceDso.V1 dso,
        int expectedVersion,
        IReadOnlyList<(string Name, Guid Id)> scopesToLink,
        IReadOnlyList<(string Name, Guid Id)> scopesToUnlink,
        Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            UpdateOperation.For(id, dso, expectedVersion, BuildKeys(dso), BuildSearchFields(dso), Expiration.NoExpiration)
        };

        // index 0 is the UpdateOperation above; everything after it is a link operation naming a scope.
        var scopeOperationNames = new List<string?> { null };

        foreach (var scope in scopesToUnlink)
        {
            operations.Add(UnlinkOperation.For(ApiResourceLinkDefinitions.ApiResourceScope, id, UuidV7.From(scope.Id)));
            scopeOperationNames.Add(scope.Name);
        }

        foreach (var scope in scopesToLink)
        {
            operations.Add(LinkOperation.For(ApiResourceLinkDefinitions.ApiResourceScope, id, UuidV7.From(scope.Id)));
            scopeOperationNames.Add(scope.Name);
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToUpdateOutcome(batchResult, scopeOperationNames);
    }

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await GetPartitionedStorage(ct)).DeleteAsync(ApiResourceDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<QueryResult<ApiResourceDso.V1>> QueryAsync(
        QueryRequest<ApiResourceFilter, ApiResourceSortField> request,
        Ct ct)
    {
        var filterValue = request.Filter?.FilterValue;
        IQueryFilterExpression? scopeLinkFilter = null;
        if (!string.IsNullOrWhiteSpace(filterValue?.Scope))
        {
            scopeLinkFilter = await BuildExactScopeNameLinkFilterAsync(filterValue.Scope, ct);
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var filter = BuildFilter(filterValue, scopeLinkFilter);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await partitionedStorage.QueryAsync(
            ApiResourceDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => ApiResourceDso.ToCurrent(e.Value));
    }

    internal async Task<List<ApiResourceDso.V1>> FindByNamesAsync(IEnumerable<string> names, Ct ct)
    {
        var nameSet = new HashSet<string>(names, StringComparer.Ordinal);

        if (nameSet.Count == 0)
        {
            return [];
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var found = new List<ApiResourceDso.V1>();

        foreach (var chunk in nameSet.Chunk(NameFilterLimits.MaxNamesPerQuery))
        {
            var filter = Fields.Name.In(chunk);

            var page = 1;
            bool hasMoreData;
            do
            {
                var result = await partitionedStorage.QueryAsync(
                    ApiResourceDso.EntityType,
                    filter,
                    new SortParameter(Fields.Name),
                    DataRange.FromPage(page++, DataRangeSize.MaxValue),
                    ct);

                // Storage string filters ignore case, but API resource names are unique only by exact match,
                // so keep only exact ordinal matches.
                found.AddRange(result.Items
                    .Select(e => ApiResourceDso.ToCurrent(e.Value))
                    .Where(dso => nameSet.Contains(dso.Name)));

                hasMoreData = result.HasMoreData;
            }
            while (hasMoreData);
        }

        return found.OrderBy(dso => dso.Name, StringComparer.Ordinal).ToList();
    }

    internal async Task<List<ApiScopeDso.V1>> GetScopesAsync(Guid resourceId, Ct ct) =>
        await QueryAllLinkedAsync(ScopesByResourceQuery(resourceId), e => ApiScopeDso.ToCurrent(e.Value), ct);

    /// <summary>
    /// Loads the scopes of many API resources in one query per page of links.
    /// Every requested id has an entry, which is empty when the API resource has no scopes.
    /// </summary>
    internal async Task<Dictionary<Guid, List<ApiScopeDso.V1>>> GetScopesByResourceAsync(IEnumerable<Guid> resourceIds, Ct ct)
    {
        var scopesByResource = resourceIds.Distinct().ToDictionary(id => id, _ => new List<ApiScopeDso.V1>());
        if (scopesByResource.Count == 0)
        {
            return scopesByResource;
        }

        var partitionedStorage = await GetPartitionedStorage(ct);

        foreach (var chunk in scopesByResource.Keys.Chunk(LinkQueryBuilder.MaxWhereAnyEntityIds))
        {
            var query = ScopesByResourcesQuery(chunk);
            var pageNumber = 1;

            while (true)
            {
                var page = await partitionedStorage.QueryLinksByAnchorAsync(
                    query, DataRange.FromPage(pageNumber, (DataRangeSize)LinkPageSize), ct);

                foreach (var item in page.Items)
                {
                    scopesByResource[item.AnchorId.Value].Add(ApiScopeDso.ToCurrent(item.Entity.Value));
                }

                if (!page.HasMoreData)
                {
                    break;
                }

                pageNumber++;
            }
        }

        return scopesByResource;
    }

    /// <summary>
    /// Counts the scopes of many API resources in one query. Every requested id has an entry.
    /// </summary>
    internal async Task<Dictionary<Guid, int>> CountScopesByResourceAsync(IEnumerable<Guid> resourceIds, Ct ct)
    {
        var ids = resourceIds.Distinct().ToList();
        var counts = new Dictionary<Guid, int>(ids.Count);
        if (ids.Count == 0)
        {
            return counts;
        }

        var partitionedStorage = await GetPartitionedStorage(ct);

        foreach (var chunk in ids.Chunk(LinkQueryBuilder.MaxWhereAnyEntityIds))
        {
            var result = await partitionedStorage.CountLinksAsync(ScopesByResourcesQuery(chunk), ct);
            foreach (var (anchorId, count) in result)
            {
                counts[anchorId.Value] = (int)count;
            }
        }

        return counts;
    }

    internal async Task<QueryResult<ApiResourceDso.V1>> QueryByScopeIdAsync(Guid scopeId, DataRange range, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.QueryLinksAsync(ResourcesByScopeQuery(scopeId), range, ct);
        return result.ConvertTo(e => ApiResourceDso.ToCurrent(e.Value));
    }

    /// <summary>
    /// Finds all API resources linked to any of the API scopes with the given exact, case-sensitive names.
    /// </summary>
    internal async Task<List<ApiResourceDso.V1>> FindByScopeNamesAsync(IReadOnlyCollection<string> scopeNames, Ct ct)
    {
        var scopesByName = await apiScopeRepository.FindByExactNamesAsync(scopeNames, ct);
        if (scopesByName.Count == 0)
        {
            return [];
        }

        var scopeIds = scopesByName.Values.Select(s => s.Id).ToList();
        var filter = LinkFilter.ToRight(ApiResourceLinkDefinitions.ApiResourceScope, ApiScopeRepository.Fields.Id.In(scopeIds));

        var partitionedStorage = await GetPartitionedStorage(ct);
        var resultsById = new Dictionary<Guid, ApiResourceDso.V1>();
        var pageNumber = 1;

        while (true)
        {
            var page = await partitionedStorage.QueryAsync(
                ApiResourceDso.EntityType,
                filter,
                new SortParameter(Fields.Name),
                DataRange.FromPage(pageNumber, (DataRangeSize)LinkPageSize),
                ct);

            foreach (var item in page.Items)
            {
                var resourceDso = ApiResourceDso.ToCurrent(item.Value);
                resultsById[resourceDso.Id] = resourceDso;
            }

            if (!page.HasMoreData)
            {
                return resultsById.Values.ToList();
            }

            pageNumber++;
        }
    }

    private static LinkQueryDescriptor ScopesByResourceQuery(Guid resourceId) =>
        StorageQuerying.LinkQuery
            .From(ApiScopeDso.EntityType)
            .Join(ApiResourceLinkDefinitions.ApiResourceScope)
            .Where(ApiResourceDso.EntityType, UuidV7.From(resourceId))
            .Build();

    private static LinkQueryDescriptor ScopesByResourcesQuery(IEnumerable<Guid> resourceIds) =>
        StorageQuerying.LinkQuery
            .From(ApiScopeDso.EntityType)
            .Join(ApiResourceLinkDefinitions.ApiResourceScope)
            .WhereAny(ApiResourceDso.EntityType, resourceIds.Select(UuidV7.From).ToHashSet())
            .Build();

    private static LinkQueryDescriptor ResourcesByScopeQuery(Guid scopeId) =>
        StorageQuerying.LinkQuery
            .From(ApiResourceDso.EntityType)
            .Join(ApiResourceLinkDefinitions.ApiResourceScope)
            .Where(ApiScopeDso.EntityType, UuidV7.From(scopeId))
            .Build();

    private async Task<List<TDso>> QueryAllLinkedAsync<TDso>(LinkQueryDescriptor query, Func<MetadataEnvelope, TDso> toCurrent, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var items = new List<TDso>();
        var pageNumber = 1;

        while (true)
        {
            var result = await partitionedStorage.QueryLinksAsync(query, DataRange.FromPage(pageNumber, (DataRangeSize)LinkPageSize), ct);
            items.AddRange(result.Items.Select(toCurrent));

            if (!result.HasMoreData)
            {
                return items;
            }

            pageNumber++;
        }
    }

    /// <summary>
    /// Builds a filter expression that matches API resources linked to the API scope with the given exact,
    /// case-sensitive name. If no scope has that exact name, the returned filter never matches, so the query
    /// still runs through the normal paged path (continuation tokens stay valid).
    /// </summary>
    private async Task<IQueryFilterExpression> BuildExactScopeNameLinkFilterAsync(string scopeName, Ct ct)
    {
        var scope = await apiScopeRepository.TryReadByNameAsync(scopeName, ct);
        if (scope is null)
        {
            return Fields.ApiResourceId.In(Array.Empty<Guid>());
        }

        return LinkFilter.ToRight(ApiResourceLinkDefinitions.ApiResourceScope, ApiScopeRepository.Fields.Id.Equals(scope.Value.Dso.Id));
    }

    private static IReadOnlyList<DataStorageKey> BuildKeys(ApiResourceDso.V1 dso) =>
        [DataStorageKey.Create(ApiResourceNameDskV1.Create(dso.Name))];

    private static SearchFieldCollection BuildSearchFields(ApiResourceDso.V1 dso) =>
        new SearchFieldsBuilder()
            .Add(Fields.Name.Path, dso.Name)
            .Add(Fields.Enabled.Path, dso.Enabled)
            .Add(Fields.ApiResourceId.Path, dso.Id)
            .Build();

    private static ApiResourceBatchOutcome MapBatchToCreateOutcome(BatchResult batchResult, IReadOnlyList<(string Name, Guid Id)> scopes)
    {
        if (batchResult.Success)
        {
            return ApiResourceBatchOutcome.Success;
        }

        var failure = FirstFailure(batchResult);

        // Index 0 is the CreateOperation for the resource itself; anything after it is a scope link.
        if (failure.Index == 0)
        {
            return failure.Outcome switch
            {
                OperationOutcome.AlreadyExists => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.AlreadyExists),
                _ => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.KeyConflict)
            };
        }

        var scopeName = scopes[failure.Index - 1].Name;
        return failure.Outcome switch
        {
            OperationOutcome.DoesNotExist => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.ScopeDoesNotExist, scopeName),
            _ => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.KeyConflict)
        };
    }

    private static ApiResourceBatchOutcome MapBatchToUpdateOutcome(BatchResult batchResult, List<string?> scopeOperationNames)
    {
        if (batchResult.Success)
        {
            return ApiResourceBatchOutcome.Success;
        }

        var failure = FirstFailure(batchResult);

        // Index 0 is the UpdateOperation for the resource itself; anything after it is a scope link/unlink.
        if (failure.Index == 0)
        {
            return failure.Outcome switch
            {
                OperationOutcome.DoesNotExist => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.ResourceDoesNotExist),
                OperationOutcome.UnexpectedVersion => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.UnexpectedVersion),
                _ => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.KeyConflict)
            };
        }

        var scopeName = scopeOperationNames[failure.Index];
        return failure.Outcome switch
        {
            OperationOutcome.DoesNotExist => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.ScopeDoesNotExist, scopeName),
            _ => new ApiResourceBatchOutcome(ApiResourceBatchOutcomeKind.KeyConflict)
        };
    }

    private static OperationResult FirstFailure(BatchResult batchResult) =>
        batchResult.Results.First(r => r.Outcome is not (OperationOutcome.Success or OperationOutcome.AlreadyLinked));

    private static IQueryExpression BuildFilter(ApiResourceFilter? filter, IQueryFilterExpression? scopeLinkFilter)
    {
        if (filter is null && scopeLinkFilter is null)
        {
            return AllExpression.Instance;
        }

        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter?.Name))
        {
            expressions.Add(Fields.Name.Contains(filter.Name));
        }

        if (filter?.Enabled is not null)
        {
            expressions.Add(Fields.Enabled.Equals(filter.Enabled.Value));
        }

        if (scopeLinkFilter is not null)
        {
            expressions.Add(scopeLinkFilter);
        }

        if (expressions.Count == 0)
        {
            return AllExpression.Instance;
        }

        if (expressions.Count == 1)
        {
            return expressions[0];
        }

        var result = expressions[0];
        for (var i = 1; i < expressions.Count; i++)
        {
            result = result.And(expressions[i]);
        }

        return result;
    }

    private static SortParameter BuildSort(SortBy.SortByField<ApiResourceSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(Fields.Name);
        }

        Field field = sort.Field switch
        {
            ApiResourceSortField.Name => Fields.Name,
            ApiResourceSortField.Enabled => Fields.Enabled,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, "Unknown sort field")
        };

        var direction = sort.Direction switch
        {
            SortDirection.Ascending => StorageSortDirection.Ascending,
            SortDirection.Descending => StorageSortDirection.Descending,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Direction, "Unknown sort direction")
        };

        return new SortParameter(field, direction);
    }
}
