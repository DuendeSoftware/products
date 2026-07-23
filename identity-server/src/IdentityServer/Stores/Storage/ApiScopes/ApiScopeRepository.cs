// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiScopes;
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

namespace Duende.IdentityServer.Stores.Storage.ApiScopes;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiScopeRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        Name = 1
    }

    internal static class Fields
    {
        public static readonly StringField Name = new("Name");
        public static readonly BooleanField Enabled = new("Enabled");
    }

    internal async Task<CreateResult> CreateAsync(UuidV7 id, ApiScopeDso.V1 dso, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(ApiScopeNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<(ApiScopeDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(ApiScopeDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? ((ApiScopeDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<(ApiScopeDso.V1 Dso, int Version)?> TryReadByNameAsync(string name, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            ApiScopeDso.EntityType,
            DataStorageKey.Create(ApiScopeNameDskV1.Create(name)),
            ct);
        return result.Found ? ((ApiScopeDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 id, ApiScopeDso.V1 dso, int expectedVersion, Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [DataStorageKey.Create(ApiScopeNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(ApiScopeDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<QueryResult<ApiScopeDso.V1>> QueryAsync(
        QueryRequest<ApiScopeFilter, ApiScopeSortField> request,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filter = BuildFilter(request.Filter?.FilterValue);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await store.QueryAsync<ApiScopeDso.V1>(
            ApiScopeDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => e.Value);
    }

    internal async Task<List<ApiScopeDso.V1>> FindByNamesAsync(IEnumerable<string> names, Ct ct)
    {
        var nameList = names.Distinct(StringComparer.Ordinal).ToList();

        if (nameList.Count == 0)
        {
            return [];
        }

        var store = await storeFactory.GetStore(ct);
        var filter = Fields.Name.In(nameList);

        var result = await store.QueryAsync<ApiScopeDso.V1>(
            ApiScopeDso.EntityType,
            filter,
            new SortParameter(Fields.Name),
            // Capped at 1000 — exceeding this many scopes is implausible in practice.
            DataRange.FromPage(1, (DataRangeSize)1000),
            ct);

        return result.Items.Select(e => e.Value).ToList();
    }

    private static SearchFieldCollection BuildSearchFields(ApiScopeDso.V1 dso) =>
        new SearchFieldsBuilder()
            .Add(Fields.Name.Path, dso.Name)
            .Add(Fields.Enabled.Path, dso.Enabled)
            .Build();

    private static IQueryExpression BuildFilter(ApiScopeFilter? filter)
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

    private static SortParameter BuildSort(SortBy.SortByField<ApiScopeSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(Fields.Name);
        }

        Field field = sort.Field switch
        {
            ApiScopeSortField.Name => Fields.Name,
            ApiScopeSortField.Enabled => Fields.Enabled,
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
