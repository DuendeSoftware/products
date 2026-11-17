// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;

namespace Duende.IdentityServer.Stores.Storage.SamlServiceProviders;

/// <summary>
/// An allowed scope for a SAML service provider, naming an identity resource by id. SAML service
/// providers can only allow identity resources, unlike clients which can also allow API scopes.
/// </summary>
internal readonly record struct SamlAllowedScopeLink(string Name, Guid Id);

/// <summary>
/// The outcome of a batch operation that creates or updates a SAML service provider together with its
/// allowed-scope links.
/// </summary>
internal enum SamlServiceProviderBatchOutcomeKind
{
    Success,
    AlreadyExists,
    KeyConflict,
    DoesNotExist,
    UnexpectedVersion,
    ScopeDoesNotExist
}

/// <summary>
/// The outcome of a batch operation, naming the scope when <see cref="Kind"/> is
/// <see cref="SamlServiceProviderBatchOutcomeKind.ScopeDoesNotExist"/>.
/// </summary>
internal readonly record struct SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind Kind, string? ScopeName = null)
{
    public static readonly SamlServiceProviderBatchOutcome Success = new(SamlServiceProviderBatchOutcomeKind.Success);
}

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SamlServiceProviderRepository(IPartitionedStorageFactory partitionedStorageFactory)
{
    private const int LinkPageSize = 200;

    private Task<IPartitionedStorage> GetPartitionedStorage(Ct ct) => partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Configuration, ct);

    internal enum Keys
    {
        EntityId = 1
    }

    private static class Fields
    {
        public static readonly StringField EntityId = new("EntityId");
        public static readonly StringField DisplayName = new("DisplayName");
        public static readonly BooleanField Enabled = new("Enabled");
    }

    internal async Task<SamlServiceProviderBatchOutcome> CreateAsync(UuidV7 id, SamlServiceProviderDso.V1 dso, IReadOnlyList<SamlAllowedScopeLink> scopes, Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            CreateOperation.For(id, dso, [DataStorageKey.Create(SamlServiceProviderEntityIdDskV1.Create(dso.EntityId))], BuildSearchFields(dso), Expiration.NoExpiration)
        };

        foreach (var scope in scopes)
        {
            operations.Add(LinkOperation.For(SamlServiceProviderLinkDefinitions.SamlServiceProviderAllowedIdentityResource, id, UuidV7.From(scope.Id)));
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToCreateOutcome(batchResult, scopes);
    }

    internal async Task<(SamlServiceProviderDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(SamlServiceProviderDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? (SamlServiceProviderDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(SamlServiceProviderDso.V1 Dso, int Version)?> TryReadByEntityIdAsync(string entityId, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(
            SamlServiceProviderDso.EntityType,
            DataStorageKey.Create(SamlServiceProviderEntityIdDskV1.Create(entityId)),
            ct);
        return result.Found ? (SamlServiceProviderDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<SamlServiceProviderBatchOutcome> UpdateAsync(
        UuidV7 id,
        SamlServiceProviderDso.V1 dso,
        int expectedVersion,
        IReadOnlyList<SamlAllowedScopeLink> scopesToLink,
        IReadOnlyList<SamlAllowedScopeLink> scopesToUnlink,
        Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            UpdateOperation.For(id, dso, expectedVersion, [DataStorageKey.Create(SamlServiceProviderEntityIdDskV1.Create(dso.EntityId))], BuildSearchFields(dso), Expiration.NoExpiration)
        };

        // index 0 is the UpdateOperation above; everything after it is a scope link operation.
        var scopeOperationNames = new List<string?> { null };

        foreach (var scope in scopesToUnlink)
        {
            operations.Add(UnlinkOperation.For(SamlServiceProviderLinkDefinitions.SamlServiceProviderAllowedIdentityResource, id, UuidV7.From(scope.Id)));
            scopeOperationNames.Add(scope.Name);
        }

        foreach (var scope in scopesToLink)
        {
            operations.Add(LinkOperation.For(SamlServiceProviderLinkDefinitions.SamlServiceProviderAllowedIdentityResource, id, UuidV7.From(scope.Id)));
            scopeOperationNames.Add(scope.Name);
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToUpdateOutcome(batchResult, scopeOperationNames);
    }

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await GetPartitionedStorage(ct)).DeleteAsync(SamlServiceProviderDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<QueryResult<SamlServiceProviderDso.V1>> QueryAsync(
        QueryRequest<SamlServiceProviderFilter, SamlServiceProviderSortField> request,
        Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var filter = BuildFilter(request.Filter?.FilterValue);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await partitionedStorage.QueryAsync(
            SamlServiceProviderDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => SamlServiceProviderDso.ToCurrent(e.Value));
    }

    /// <summary>
    /// Gets the names of every identity resource the SAML service provider is allowed, sorted ordinally.
    /// </summary>
    internal async Task<List<string>> GetAllowedScopeNamesAsync(Guid serviceProviderId, Ct ct) =>
        (await QueryAllLinkedAsync(AllowedScopesQuery(serviceProviderId), e => IdentityResourceDso.ToCurrent(e.Value).Name, ct))
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Gets the SAML service provider's current allowed-scope links, for diffing against a requested set.
    /// </summary>
    internal async Task<List<SamlAllowedScopeLink>> GetAllowedScopesAsync(Guid serviceProviderId, Ct ct) =>
        await QueryAllLinkedAsync(
            AllowedScopesQuery(serviceProviderId),
            e =>
            {
                var dso = IdentityResourceDso.ToCurrent(e.Value);
                return new SamlAllowedScopeLink(dso.Name, dso.Id);
            },
            ct);

    /// <summary>
    /// Loads the allowed-scope names of many SAML service providers in one query per page of links, per
    /// chunk of <see cref="LinkQueryBuilder.MaxWhereAnyEntityIds"/> ids. Every requested id has an entry
    /// (sorted ordinally), which is empty when the service provider allows no scopes.
    /// </summary>
    internal async Task<Dictionary<Guid, List<string>>> GetAllowedScopeNamesByServiceProviderIdsAsync(IEnumerable<Guid> serviceProviderIds, Ct ct)
    {
        var namesByServiceProvider = serviceProviderIds.Distinct().ToDictionary(id => id, _ => new List<string>());
        if (namesByServiceProvider.Count == 0)
        {
            return namesByServiceProvider;
        }

        var partitionedStorage = await GetPartitionedStorage(ct);

        foreach (var chunk in namesByServiceProvider.Keys.Chunk(LinkQueryBuilder.MaxWhereAnyEntityIds))
        {
            var query = AllowedScopesQuery(chunk);
            var pageNumber = 1;

            while (true)
            {
                var page = await partitionedStorage.QueryLinksByAnchorAsync(
                    query, DataRange.FromPage(pageNumber, (DataRangeSize)LinkPageSize), ct);

                foreach (var item in page.Items)
                {
                    namesByServiceProvider[item.AnchorId.Value].Add(IdentityResourceDso.ToCurrent(item.Entity.Value).Name);
                }

                if (!page.HasMoreData)
                {
                    break;
                }

                pageNumber++;
            }
        }

        foreach (var key in namesByServiceProvider.Keys.ToList())
        {
            namesByServiceProvider[key] = namesByServiceProvider[key].Order(StringComparer.Ordinal).ToList();
        }

        return namesByServiceProvider;
    }

    /// <summary>
    /// Counts, for each given SAML service provider id, how many identity resources it's allowed, chunked
    /// by <see cref="LinkQueryBuilder.MaxWhereAnyEntityIds"/> ids per command.
    /// </summary>
    internal async Task<Dictionary<Guid, int>> CountAllowedScopesByServiceProviderIdsAsync(IReadOnlyCollection<Guid> serviceProviderIds, Ct ct)
    {
        var counts = serviceProviderIds.ToDictionary(id => id, _ => 0);
        if (serviceProviderIds.Count == 0)
        {
            return counts;
        }

        var partitionedStorage = await GetPartitionedStorage(ct);

        foreach (var chunk in serviceProviderIds.Distinct().Chunk(LinkQueryBuilder.MaxWhereAnyEntityIds))
        {
            var result = await partitionedStorage.CountLinksAsync(AllowedScopesQuery(chunk), ct);
            foreach (var (anchorId, count) in result)
            {
                counts[anchorId.Value] = (int)count;
            }
        }

        return counts;
    }

    /// <summary>
    /// Finds all SAML service providers that allow the identity resource with the given id, for use in a
    /// delete-in-use error.
    /// </summary>
    internal async Task<QueryResult<SamlServiceProviderDso.V1>> QueryByAllowedIdentityResourceIdAsync(Guid identityResourceId, DataRange range, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var query = StorageQuerying.LinkQuery
            .From(SamlServiceProviderDso.EntityType)
            .Join(SamlServiceProviderLinkDefinitions.SamlServiceProviderAllowedIdentityResource)
            .Where(IdentityResourceDso.EntityType, UuidV7.From(identityResourceId))
            .Build();
        var result = await partitionedStorage.QueryLinksAsync(query, range, ct);
        return result.ConvertTo(e => SamlServiceProviderDso.ToCurrent(e.Value));
    }

    private static LinkQueryDescriptor AllowedScopesQuery(Guid serviceProviderId) =>
        StorageQuerying.LinkQuery
            .From(IdentityResourceDso.EntityType)
            .Join(SamlServiceProviderLinkDefinitions.SamlServiceProviderAllowedIdentityResource)
            .Where(SamlServiceProviderDso.EntityType, UuidV7.From(serviceProviderId))
            .Build();

    private static LinkQueryDescriptor AllowedScopesQuery(IEnumerable<Guid> serviceProviderIds) =>
        StorageQuerying.LinkQuery
            .From(IdentityResourceDso.EntityType)
            .Join(SamlServiceProviderLinkDefinitions.SamlServiceProviderAllowedIdentityResource)
            .WhereAny(SamlServiceProviderDso.EntityType, serviceProviderIds.Select(UuidV7.From).ToHashSet())
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

    private static SamlServiceProviderBatchOutcome MapBatchToCreateOutcome(BatchResult batchResult, IReadOnlyList<SamlAllowedScopeLink> scopes)
    {
        if (batchResult.Success)
        {
            return SamlServiceProviderBatchOutcome.Success;
        }

        var failure = FirstFailure(batchResult);

        // Index 0 is the CreateOperation for the service provider itself; anything after it is a scope link.
        if (failure.Index == 0)
        {
            return failure.Outcome switch
            {
                OperationOutcome.AlreadyExists => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.AlreadyExists),
                _ => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.KeyConflict)
            };
        }

        var scopeName = scopes[failure.Index - 1].Name;
        return failure.Outcome switch
        {
            OperationOutcome.DoesNotExist => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.ScopeDoesNotExist, scopeName),
            _ => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.KeyConflict)
        };
    }

    private static SamlServiceProviderBatchOutcome MapBatchToUpdateOutcome(BatchResult batchResult, List<string?> scopeOperationNames)
    {
        if (batchResult.Success)
        {
            return SamlServiceProviderBatchOutcome.Success;
        }

        var failure = FirstFailure(batchResult);

        // Index 0 is the UpdateOperation for the service provider itself; anything after it is a scope link/unlink.
        if (failure.Index == 0)
        {
            return failure.Outcome switch
            {
                OperationOutcome.DoesNotExist => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.DoesNotExist),
                OperationOutcome.UnexpectedVersion => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.UnexpectedVersion),
                OperationOutcome.KeyConflict => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.KeyConflict),
                _ => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.KeyConflict)
            };
        }

        var scopeName = scopeOperationNames[failure.Index];
        return failure.Outcome switch
        {
            OperationOutcome.DoesNotExist => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.ScopeDoesNotExist, scopeName),
            _ => new SamlServiceProviderBatchOutcome(SamlServiceProviderBatchOutcomeKind.KeyConflict)
        };
    }

    private static OperationResult FirstFailure(BatchResult batchResult) =>
        batchResult.Results.First(r => r.Outcome is not (OperationOutcome.Success or OperationOutcome.AlreadyLinked));

    private static SearchFieldCollection BuildSearchFields(SamlServiceProviderDso.V1 dso)
    {
        var builder = new SearchFieldsBuilder()
            .Add(Fields.EntityId.Path, dso.EntityId)
            .Add(Fields.Enabled.Path, dso.Enabled);

        if (dso.DisplayName is not null)
        {
            _ = builder.Add(Fields.DisplayName.Path, dso.DisplayName);
        }

        return builder.Build();
    }

    private static IQueryExpression BuildFilter(SamlServiceProviderFilter? filter)
    {
        if (filter is null)
        {
            return AllExpression.Instance;
        }

        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            expressions.Add(Fields.EntityId.Contains(filter.EntityId));
        }

        if (!string.IsNullOrWhiteSpace(filter.DisplayName))
        {
            expressions.Add(Fields.DisplayName.Contains(filter.DisplayName));
        }

        if (filter.Enabled.HasValue)
        {
            expressions.Add(Fields.Enabled.Equals(filter.Enabled.Value));
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

    private static SortParameter BuildSort(SortBy.SortByField<SamlServiceProviderSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(Fields.EntityId);
        }

        Field field = sort.Field switch
        {
            SamlServiceProviderSortField.EntityId => Fields.EntityId,
            SamlServiceProviderSortField.DisplayName => Fields.DisplayName,
            SamlServiceProviderSortField.Enabled => Fields.Enabled,
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
