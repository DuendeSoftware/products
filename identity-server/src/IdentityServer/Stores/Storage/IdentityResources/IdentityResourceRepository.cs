// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.IdentityResources;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;

namespace Duende.IdentityServer.Stores.Storage.IdentityResources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class IdentityResourceRepository(IPartitionedStorageFactory partitionedStorageFactory)
{
    private Task<IPartitionedStorage> GetPartitionedStorage(Ct ct) => partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Configuration, ct);

    internal enum Keys
    {
        Name = 1
    }

    internal static class Fields
    {
        public static readonly StringField Name = new("Name");
        public static readonly BooleanField Enabled = new("Enabled");
        public static readonly GuidField Id = new("Id");
    }

    internal async Task<ScopeNameBatchOutcome> CreateAsync(UuidV7 id, IdentityResourceDso.V1 dso, Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            CreateOperation.For(
                id,
                dso,
                [DataStorageKey.Create(IdentityResourceNameDskV1.Create(dso.Name))],
                BuildSearchFields(dso),
                Expiration.NoExpiration),
            ScopeNameRepository.CreateGuardOperation(id, dso.Name)
        };

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return ScopeNameRepository.MapBatchOutcome(operations, batchResult);
    }

    internal async Task<(IdentityResourceDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(IdentityResourceDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? (IdentityResourceDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(IdentityResourceDso.V1 Dso, int Version)?> TryReadByNameAsync(string name, Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var result = await partitionedStorage.TryReadAsync(
            IdentityResourceDso.EntityType,
            DataStorageKey.Create(IdentityResourceNameDskV1.Create(name)),
            ct);
        return result.Found ? (IdentityResourceDso.ToCurrent(result.Dso), result.Version.Value) : null;
    }

    internal async Task<ScopeNameBatchOutcome> UpdateAsync(UuidV7 id, IdentityResourceDso.V1 dso, int expectedVersion, string previousName, Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            UpdateOperation.For(
                id,
                dso,
                expectedVersion,
                [DataStorageKey.Create(IdentityResourceNameDskV1.Create(dso.Name))],
                BuildSearchFields(dso),
                Expiration.NoExpiration)
        };

        if (!string.Equals(previousName, dso.Name, StringComparison.Ordinal))
        {
            // Delete by ID, not by the old name key: a row saved before the guard existed has no guard
            // of its own, so deleting by the old name's key could remove a guard the other kind
            // legitimately holds for that name. Deleting by this entity's own ID only ever removes its
            // own guard (a no-op when it never had one).
            operations.Add(ScopeNameRepository.DeleteGuardOperation(id));
            operations.Add(ScopeNameRepository.CreateGuardOperation(id, dso.Name));
        }

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return ScopeNameRepository.MapBatchOutcome(operations, batchResult);
    }

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct)
    {
        var operations = new List<IStorageOperation>
        {
            DeleteOperation.ById(IdentityResourceDso.EntityType, UuidV7.From(id)),
            ScopeNameRepository.DeleteGuardOperation(UuidV7.From(id))
        };

        var partitionedStorage = await GetPartitionedStorage(ct);
        var batchResult = await partitionedStorage.ExecuteBatchAsync(operations, [], ct);
        return batchResult.Results[0].Outcome switch
        {
            OperationOutcome.Success => DeleteResult.Success,
            OperationOutcome.StillLinked => DeleteResult.StillLinked,
            _ => throw new InvalidOperationException($"Unexpected OperationOutcome: {batchResult.Results[0].Outcome}")
        };
    }

    internal async Task<QueryResult<IdentityResourceDso.V1>> QueryAsync(
        QueryRequest<IdentityResourceFilter, IdentityResourceSortField> request,
        Ct ct)
    {
        var partitionedStorage = await GetPartitionedStorage(ct);
        var filter = BuildFilter(request.Filter?.FilterValue);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await partitionedStorage.QueryAsync(
            IdentityResourceDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => IdentityResourceDso.ToCurrent(e.Value));
    }

    internal async Task<Dictionary<string, IdentityResourceDso.V1>> FindByExactNamesAsync(IEnumerable<string> names, Ct ct)
    {
        var nameSet = new HashSet<string>(names, StringComparer.Ordinal);
        var found = new Dictionary<string, IdentityResourceDso.V1>(StringComparer.Ordinal);

        if (nameSet.Count == 0)
        {
            return found;
        }

        var partitionedStorage = await GetPartitionedStorage(ct);

        foreach (var chunk in nameSet.Chunk(NameFilterLimits.MaxNamesPerQuery))
        {
            var filter = Fields.Name.In(chunk);

            var page = 1;
            bool hasMoreData;
            do
            {
                var result = await partitionedStorage.QueryAsync(
                    IdentityResourceDso.EntityType,
                    filter,
                    SortParameter.Empty,
                    DataRange.FromPage(page++, DataRangeSize.MaxValue),
                    ct);

                foreach (var entity in result.Items)
                {
                    // Storage string filters ignore case, so keep only exact ordinal matches.
                    var dso = IdentityResourceDso.ToCurrent(entity.Value);
                    if (nameSet.Contains(dso.Name))
                    {
                        found[dso.Name] = dso;
                    }
                }

                hasMoreData = result.HasMoreData;
            }
            while (hasMoreData);
        }

        return found;
    }

    private static SearchFieldCollection BuildSearchFields(IdentityResourceDso.V1 dso) =>
        new SearchFieldsBuilder()
            .Add(Fields.Name.Path, dso.Name)
            .Add(Fields.Enabled.Path, dso.Enabled)
            .Add(Fields.Id.Path, dso.Id)
            .Build();

    private static IQueryExpression BuildFilter(IdentityResourceFilter? filter)
    {
        if (filter is null)
        {
            return AllExpression.Instance;
        }

        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            expressions.Add(Fields.Name.Contains(filter.Name));
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

    private static SortParameter BuildSort(SortBy.SortByField<IdentityResourceSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(Fields.Name);
        }

        Field field = sort.Field switch
        {
            IdentityResourceSortField.Name => Fields.Name,
            IdentityResourceSortField.Enabled => Fields.Enabled,
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
