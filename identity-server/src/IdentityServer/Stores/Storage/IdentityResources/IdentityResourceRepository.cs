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
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using StorageSortDirection = Duende.Storage.Querying.SortDirection;

namespace Duende.IdentityServer.Stores.Storage.IdentityResources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class IdentityResourceRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        Name = 1
    }

    private static class Fields
    {
        public static readonly StringField Name = new("Name");
        public static readonly BooleanField Enabled = new("Enabled");
    }

    internal async Task<CreateResult> CreateAsync(UuidV7 id, IdentityResourceDso.V1 dso, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(IdentityResourceNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<(IdentityResourceDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(IdentityResourceDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? ((IdentityResourceDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<(IdentityResourceDso.V1 Dso, int Version)?> TryReadByNameAsync(string name, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            IdentityResourceDso.EntityType,
            DataStorageKey.Create(IdentityResourceNameDskV1.Create(name)),
            ct);
        return result.Found ? ((IdentityResourceDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 id, IdentityResourceDso.V1 dso, int expectedVersion, Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [DataStorageKey.Create(IdentityResourceNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(IdentityResourceDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<QueryResult<IdentityResourceDso.V1>> QueryAsync(
        QueryRequest<IdentityResourceFilter, IdentityResourceSortField> request,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filter = BuildFilter(request.Filter?.FilterValue);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await store.QueryAsync<IdentityResourceDso.V1>(
            IdentityResourceDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => e.Value);
    }

    internal async Task<List<IdentityResourceDso.V1>> FindByNamesAsync(IEnumerable<string> names, Ct ct)
    {
        var nameList = names.Distinct(StringComparer.Ordinal).ToList();

        if (nameList.Count == 0)
        {
            return [];
        }

        var store = await storeFactory.GetStore(ct);
        var filter = Fields.Name.In(nameList);

        var result = await store.QueryAsync<IdentityResourceDso.V1>(
            IdentityResourceDso.EntityType,
            filter,
            new SortParameter(Fields.Name),
            // Capped at 1000 — exceeding this many identity resources is implausible in practice.
            DataRange.FromPage(1, (DataRangeSize)1000),
            ct);

        return result.Items.Select(e => e.Value).ToList();
    }

    private static SearchFieldCollection BuildSearchFields(IdentityResourceDso.V1 dso) =>
        new SearchFieldsBuilder()
            .Add(Fields.Name.Path, dso.Name)
            .Add(Fields.Enabled.Path, dso.Enabled)
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
