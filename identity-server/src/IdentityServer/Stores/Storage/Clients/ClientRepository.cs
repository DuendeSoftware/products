// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.IdentityServer.Stores.Storage.IdentityResources;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;

namespace Duende.IdentityServer.Stores.Storage.Clients;

/// <summary>
/// The kind of resource an allowed-scope link targets.
/// </summary>
internal enum ScopeLinkKind
{
    ApiScope,
    IdentityResource
}

/// <summary>
/// A client's allowed scope, naming an API scope or identity resource by id.
/// </summary>
internal readonly record struct ScopeLink(string Name, Guid Id, ScopeLinkKind Kind);

/// <summary>
/// The outcome of a batch operation that creates or updates a client together with its allowed-scope links.
/// </summary>
internal enum ClientBatchOutcomeKind
{
    Success,
    AlreadyExists,
    KeyConflict,
    ClientDoesNotExist,
    UnexpectedVersion,
    ScopeDoesNotExist
}

/// <summary>
/// The outcome of a batch operation, naming the scope when <see cref="Kind"/> is
/// <see cref="ClientBatchOutcomeKind.ScopeDoesNotExist"/>.
/// </summary>
internal readonly record struct ClientBatchOutcome(ClientBatchOutcomeKind Kind, string? ScopeName = null)
{
    public static readonly ClientBatchOutcome Success = new(ClientBatchOutcomeKind.Success);
}

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ClientRepository(
    IPartitionedStorageFactory partitionedStorageFactory,
    ApiScopeRepository apiScopeRepository,
    IdentityResourceRepository identityResourceRepository)
{
    private const int LinkPageSize = 200;

    private Task<IPartitionedStorage> GetPartitionedStorage(Ct ct) => partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Configuration, ct);

    internal enum Keys
    {
        ClientId = 1
    }

    private static class Fields
    {
        public static readonly StringField ClientId = new("ClientId");
        public static readonly StringField ClientName = new("ClientName");
        public static readonly BooleanField Enabled = new("Enabled");
        public static readonly StringArrayField GrantType = new("GrantType");
        public static readonly StringArrayField AllowedCorsOrigin = new("AllowedCorsOrigin");
    }

    private static LinkDefinition LinkDefinitionFor(ScopeLinkKind kind) => kind switch
    {
        ScopeLinkKind.ApiScope => ClientLinkDefinitions.ClientAllowedApiScope,
        ScopeLinkKind.IdentityResource => ClientLinkDefinitions.ClientAllowedIdentityResource,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scope link kind")
    };

    internal async Task<ClientBatchOutcome> CreateAsync(UuidV7 id, ClientDso.V1 dso, IReadOnlyList<ScopeLink> scopes, Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            CreateOperation.For(id, dso, [DataStorageKey.Create(ClientIdDskV1.Create(dso.ClientId))], BuildSearchFields(dso), Expiration.NoExpiration)
        };

        foreach (var scope in scopes)
        {
            operations.Add(LinkOperation.For(LinkDefinitionFor(scope.Kind), id, UuidV7.From(scope.Id)));
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToCreateOutcome(batchResult, scopes);
    }

    internal async Task<(ClientDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(ClientDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? (ClientDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(ClientDso.V1 Dso, int Version)?> TryReadByClientIdAsync(string clientId, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(
            ClientDso.EntityType,
            DataStorageKey.Create(ClientIdDskV1.Create(clientId)),
            ct);
        return result.Found ? (ClientDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 id, ClientDso.V1 dso, int expectedVersion, Ct ct) =>
        await (await GetPartitionedStorage(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [DataStorageKey.Create(ClientIdDskV1.Create(dso.ClientId))],
            BuildSearchFields(dso),
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

    internal async Task<ClientBatchOutcome> UpdateAsync(
        UuidV7 id,
        ClientDso.V1 dso,
        int expectedVersion,
        IReadOnlyList<ScopeLink> scopesToLink,
        IReadOnlyList<ScopeLink> scopesToUnlink,
        Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            UpdateOperation.For(id, dso, expectedVersion, [DataStorageKey.Create(ClientIdDskV1.Create(dso.ClientId))], BuildSearchFields(dso), Expiration.NoExpiration)
        };

        // index 0 is the UpdateOperation above; everything after it is a scope link operation.
        var scopeOperationNames = new List<string?> { null };

        foreach (var scope in scopesToUnlink)
        {
            operations.Add(UnlinkOperation.For(LinkDefinitionFor(scope.Kind), id, UuidV7.From(scope.Id)));
            scopeOperationNames.Add(scope.Name);
        }

        foreach (var scope in scopesToLink)
        {
            operations.Add(LinkOperation.For(LinkDefinitionFor(scope.Kind), id, UuidV7.From(scope.Id)));
            scopeOperationNames.Add(scope.Name);
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToUpdateOutcome(batchResult, scopeOperationNames);
    }

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await GetPartitionedStorage(ct)).DeleteAsync(ClientDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<bool> HasClientWithCorsOriginAsync(string origin, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var filter = Fields.AllowedCorsOrigin.Contains(origin);
        var count = await partitionedStorage.CountAsync(ClientDso.EntityType, filter, ct);
        return count > 0;
    }

    internal async Task<QueryResult<ClientDso.V1>> QueryAsync(
        QueryRequest<ClientFilter, ClientSortField> request,
        Ct ct)
    {
        var filterValue = request.Filter?.FilterValue;
        IQueryFilterExpression? scopeLinkFilter = null;
        if (!string.IsNullOrWhiteSpace(filterValue?.AllowedScope))
        {
            scopeLinkFilter = await BuildExactAllowedScopeLinkFilterAsync(filterValue.AllowedScope, ct);
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var filter = BuildFilter(filterValue, scopeLinkFilter);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await partitionedStorage.QueryAsync(
            ClientDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => ClientDso.ToCurrent(e.Value));
    }

    /// <summary>
    /// Gets the names of every API scope and identity resource the client is allowed, sorted ordinally.
    /// </summary>
    internal async Task<List<string>> GetAllowedScopeNamesAsync(Guid clientId, Ct ct)
    {
        var apiScopes = await QueryAllLinkedAsync(ApiScopesByClientQuery(clientId), e => ApiScopeDso.ToCurrent(e.Value).Name, ct);
        var identityResources = await QueryAllLinkedAsync(IdentityResourcesByClientQuery(clientId), e => IdentityResourceDso.ToCurrent(e.Value).Name, ct);
        return apiScopes.Concat(identityResources).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Gets the client's current allowed-scope links, naming their kind, for diffing against a requested set.
    /// </summary>
    internal async Task<List<ScopeLink>> GetAllowedScopesAsync(Guid clientId, Ct ct)
    {
        var apiScopes = await QueryAllLinkedAsync(
            ApiScopesByClientQuery(clientId),
            e =>
            {
                var dso = ApiScopeDso.ToCurrent(e.Value);
                return new ScopeLink(dso.Name, dso.Id, ScopeLinkKind.ApiScope);
            },
            ct);
        var identityResources = await QueryAllLinkedAsync(
            IdentityResourcesByClientQuery(clientId),
            e =>
            {
                var dso = IdentityResourceDso.ToCurrent(e.Value);
                return new ScopeLink(dso.Name, dso.Id, ScopeLinkKind.IdentityResource);
            },
            ct);
        return [.. apiScopes, .. identityResources];
    }

    /// <summary>
    /// Counts, for each given client id, how many API scopes and identity resources it's allowed, in two
    /// batched commands per chunk of <see cref="LinkQueryBuilder.MaxWhereAnyEntityIds"/> client ids (one per
    /// resource kind) rather than one command per client.
    /// </summary>
    internal async Task<Dictionary<Guid, int>> CountAllowedScopesByClientIdsAsync(IReadOnlyCollection<Guid> clientIds, Ct ct)
    {
        var counts = clientIds.ToDictionary(id => id, _ => 0);
        if (clientIds.Count == 0)
        {
            return counts;
        }

        var partitionedStorage = await GetPartitionedStorage(ct);

        foreach (var chunk in clientIds.Distinct().Chunk(LinkQueryBuilder.MaxWhereAnyEntityIds))
        {
            var anchorIds = chunk.Select(UuidV7.From).ToHashSet();

            var apiScopeQuery = StorageQuerying.LinkQuery
                .From(ApiScopeDso.EntityType)
                .Join(ClientLinkDefinitions.ClientAllowedApiScope)
                .WhereAny(ClientDso.EntityType, anchorIds)
                .Build();
            var identityResourceQuery = StorageQuerying.LinkQuery
                .From(IdentityResourceDso.EntityType)
                .Join(ClientLinkDefinitions.ClientAllowedIdentityResource)
                .WhereAny(ClientDso.EntityType, anchorIds)
                .Build();

            var apiScopeCounts = await partitionedStorage.CountLinksAsync(apiScopeQuery, ct);
            var identityResourceCounts = await partitionedStorage.CountLinksAsync(identityResourceQuery, ct);

            foreach (var clientId in chunk)
            {
                var anchor = UuidV7.From(clientId);
                var apiScopeCount = apiScopeCounts.GetValueOrDefault(anchor);
                var identityResourceCount = identityResourceCounts.GetValueOrDefault(anchor);
                counts[clientId] = (int)(apiScopeCount + identityResourceCount);
            }
        }

        return counts;
    }

    /// <summary>
    /// Loads the allowed scope names of many clients in one query per page of links, per resource kind.
    /// Every requested id has an entry (sorted ordinally), which is empty when the client allows no scopes.
    /// </summary>
    internal async Task<Dictionary<Guid, List<string>>> GetAllowedScopeNamesByClientIdsAsync(IEnumerable<Guid> clientIds, Ct ct)
    {
        var namesByClient = clientIds.Distinct().ToDictionary(id => id, _ => new List<string>());
        if (namesByClient.Count == 0)
        {
            return namesByClient;
        }

        var partitionedStorage = await GetPartitionedStorage(ct);

        foreach (var chunk in namesByClient.Keys.Chunk(LinkQueryBuilder.MaxWhereAnyEntityIds))
        {
            var anchorIds = chunk.Select(UuidV7.From).ToHashSet();

            var apiScopeQuery = StorageQuerying.LinkQuery
                .From(ApiScopeDso.EntityType)
                .Join(ClientLinkDefinitions.ClientAllowedApiScope)
                .WhereAny(ClientDso.EntityType, anchorIds)
                .Build();
            var identityResourceQuery = StorageQuerying.LinkQuery
                .From(IdentityResourceDso.EntityType)
                .Join(ClientLinkDefinitions.ClientAllowedIdentityResource)
                .WhereAny(ClientDso.EntityType, anchorIds)
                .Build();

            await AddLinkedNamesAsync(partitionedStorage, apiScopeQuery, e => ApiScopeDso.ToCurrent(e.Value).Name, namesByClient, ct);
            await AddLinkedNamesAsync(partitionedStorage, identityResourceQuery, e => IdentityResourceDso.ToCurrent(e.Value).Name, namesByClient, ct);
        }

        foreach (var key in namesByClient.Keys.ToList())
        {
            namesByClient[key] = namesByClient[key].Order(StringComparer.Ordinal).ToList();
        }

        return namesByClient;
    }

    private static async Task AddLinkedNamesAsync(
        IPartitionedStorage partitionedStorage,
        LinkQueryDescriptor query,
        Func<MetadataEnvelope, string> toName,
        Dictionary<Guid, List<string>> namesByClient,
        Ct ct)
    {
        var pageNumber = 1;

        while (true)
        {
            var page = await partitionedStorage.QueryLinksByAnchorAsync(
                query, DataRange.FromPage(pageNumber, (DataRangeSize)LinkPageSize), ct);

            foreach (var item in page.Items)
            {
                namesByClient[item.AnchorId.Value].Add(toName(item.Entity));
            }

            if (!page.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }
    }

    /// <summary>
    /// Finds all clients that allow the API scope with the given id, for use in a delete-in-use error.
    /// </summary>
    internal async Task<QueryResult<ClientDso.V1>> QueryByAllowedApiScopeIdAsync(Guid scopeId, DataRange range, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var query = StorageQuerying.LinkQuery
            .From(ClientDso.EntityType)
            .Join(ClientLinkDefinitions.ClientAllowedApiScope)
            .Where(ApiScopeDso.EntityType, UuidV7.From(scopeId))
            .Build();
        var result = await partitionedStorage.QueryLinksAsync(query, range, ct);
        return result.ConvertTo(e => ClientDso.ToCurrent(e.Value));
    }

    /// <summary>
    /// Finds all clients that allow the identity resource with the given id, for use in a delete-in-use error.
    /// </summary>
    internal async Task<QueryResult<ClientDso.V1>> QueryByAllowedIdentityResourceIdAsync(Guid identityResourceId, DataRange range, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var query = StorageQuerying.LinkQuery
            .From(ClientDso.EntityType)
            .Join(ClientLinkDefinitions.ClientAllowedIdentityResource)
            .Where(IdentityResourceDso.EntityType, UuidV7.From(identityResourceId))
            .Build();
        var result = await partitionedStorage.QueryLinksAsync(query, range, ct);
        return result.ConvertTo(e => ClientDso.ToCurrent(e.Value));
    }

    private static LinkQueryDescriptor ApiScopesByClientQuery(Guid clientId) =>
        StorageQuerying.LinkQuery
            .From(ApiScopeDso.EntityType)
            .Join(ClientLinkDefinitions.ClientAllowedApiScope)
            .Where(ClientDso.EntityType, UuidV7.From(clientId))
            .Build();

    private static LinkQueryDescriptor IdentityResourcesByClientQuery(Guid clientId) =>
        StorageQuerying.LinkQuery
            .From(IdentityResourceDso.EntityType)
            .Join(ClientLinkDefinitions.ClientAllowedIdentityResource)
            .Where(ClientDso.EntityType, UuidV7.From(clientId))
            .Build();

    private async Task<List<TResult>> QueryAllLinkedAsync<TResult>(LinkQueryDescriptor query, Func<MetadataEnvelope, TResult> project, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var items = new List<TResult>();
        var pageNumber = 1;

        while (true)
        {
            var result = await partitionedStorage.QueryLinksAsync(query, DataRange.FromPage(pageNumber, (DataRangeSize)LinkPageSize), ct);
            items.AddRange(result.Items.Select(project));

            if (!result.HasMoreData)
            {
                return items;
            }

            pageNumber++;
        }
    }

    /// <summary>
    /// Builds a filter expression that matches clients allowed the API scope or identity resource with the
    /// given exact, case-sensitive name. If no resource of either kind has that exact name, the returned
    /// filter never matches, so the query still runs through the normal paged path.
    /// </summary>
    private async Task<IQueryFilterExpression> BuildExactAllowedScopeLinkFilterAsync(string scopeName, Ct ct)
    {
        var apiScope = await apiScopeRepository.TryReadByNameAsync(scopeName, ct);
        var identityResource = await identityResourceRepository.TryReadByNameAsync(scopeName, ct);

        var apiScopeFilter = apiScope is not null
            ? LinkFilter.ToRight(ClientLinkDefinitions.ClientAllowedApiScope, ApiScopeRepository.Fields.Id.Equals(apiScope.Value.Dso.Id))
            : null;
        var identityResourceFilter = identityResource is not null
            ? LinkFilter.ToRight(ClientLinkDefinitions.ClientAllowedIdentityResource, IdentityResourceRepository.Fields.Id.Equals(identityResource.Value.Dso.Id))
            : null;

        return (apiScopeFilter, identityResourceFilter) switch
        {
            (not null, not null) => apiScopeFilter.Or(identityResourceFilter),
            (not null, null) => apiScopeFilter,
            (null, not null) => identityResourceFilter,
            (null, null) => Fields.ClientId.In(Array.Empty<string>())
        };
    }

    private static SearchFieldCollection BuildSearchFields(ClientDso.V1 dso)
    {
        var builder = new SearchFieldsBuilder()
            .Add(Fields.ClientId.Path, dso.ClientId)
            .Add(Fields.Enabled.Path, dso.Enabled);

        if (dso.ClientName is not null)
        {
            _ = builder.Add(Fields.ClientName.Path, dso.ClientName);
        }

        var grantTypeIndex = 0;
        foreach (var grantType in dso.AllowedGrantTypes)
        {
            _ = builder.Add(Fields.GrantType.Path, grantTypeIndex++, grantType);
        }

        var corsOriginIndex = 0;
        foreach (var origin in dso.AllowedCorsOrigins)
        {
            _ = builder.Add(Fields.AllowedCorsOrigin.Path, corsOriginIndex++, origin);
        }

        return builder.Build();
    }

    private static IQueryExpression BuildFilter(ClientFilter? filter, IQueryFilterExpression? scopeLinkFilter)
    {
        if (filter is null && scopeLinkFilter is null)
        {
            return AllExpression.Instance;
        }

        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter?.ClientId))
        {
            expressions.Add(Fields.ClientId.Contains(filter.ClientId));
        }

        if (!string.IsNullOrWhiteSpace(filter?.ClientName))
        {
            expressions.Add(Fields.ClientName.Contains(filter.ClientName));
        }

        if (filter?.Enabled is not null)
        {
            expressions.Add(Fields.Enabled.Equals(filter.Enabled.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter?.GrantType))
        {
            expressions.Add(Fields.GrantType.Contains(filter.GrantType));
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

    private static SortParameter BuildSort(SortBy.SortByField<ClientSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(Fields.ClientId);
        }

        Field field = sort.Field switch
        {
            ClientSortField.ClientId => Fields.ClientId,
            ClientSortField.ClientName => Fields.ClientName,
            ClientSortField.Enabled => Fields.Enabled,
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

    private static ClientBatchOutcome MapBatchToCreateOutcome(BatchResult batchResult, IReadOnlyList<ScopeLink> scopes)
    {
        if (batchResult.Success)
        {
            return ClientBatchOutcome.Success;
        }

        var failure = FirstFailure(batchResult);

        // Index 0 is the CreateOperation for the client itself; anything after it is a scope link.
        if (failure.Index == 0)
        {
            return failure.Outcome switch
            {
                OperationOutcome.AlreadyExists => new ClientBatchOutcome(ClientBatchOutcomeKind.AlreadyExists),
                _ => new ClientBatchOutcome(ClientBatchOutcomeKind.KeyConflict)
            };
        }

        var scopeName = scopes[failure.Index - 1].Name;
        return failure.Outcome switch
        {
            OperationOutcome.DoesNotExist => new ClientBatchOutcome(ClientBatchOutcomeKind.ScopeDoesNotExist, scopeName),
            _ => new ClientBatchOutcome(ClientBatchOutcomeKind.KeyConflict)
        };
    }

    private static ClientBatchOutcome MapBatchToUpdateOutcome(BatchResult batchResult, List<string?> scopeOperationNames)
    {
        if (batchResult.Success)
        {
            return ClientBatchOutcome.Success;
        }

        var failure = FirstFailure(batchResult);

        // Index 0 is the UpdateOperation for the client itself; anything after it is a scope link/unlink.
        if (failure.Index == 0)
        {
            return failure.Outcome switch
            {
                OperationOutcome.DoesNotExist => new ClientBatchOutcome(ClientBatchOutcomeKind.ClientDoesNotExist),
                OperationOutcome.UnexpectedVersion => new ClientBatchOutcome(ClientBatchOutcomeKind.UnexpectedVersion),
                OperationOutcome.KeyConflict => new ClientBatchOutcome(ClientBatchOutcomeKind.KeyConflict),
                _ => new ClientBatchOutcome(ClientBatchOutcomeKind.KeyConflict)
            };
        }

        var scopeName = scopeOperationNames[failure.Index];
        return failure.Outcome switch
        {
            OperationOutcome.DoesNotExist => new ClientBatchOutcome(ClientBatchOutcomeKind.ScopeDoesNotExist, scopeName),
            _ => new ClientBatchOutcome(ClientBatchOutcomeKind.KeyConflict)
        };
    }

    private static OperationResult FirstFailure(BatchResult batchResult) =>
        batchResult.Results.First(r => r.Outcome is not (OperationOutcome.Success or OperationOutcome.AlreadyLinked));
}
