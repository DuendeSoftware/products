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
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using StorageSortDirection = Duende.Storage.Querying.SortDirection;

namespace Duende.IdentityServer.Stores.Storage.SamlServiceProviders;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SamlServiceProviderRepository(IStoreFactory storeFactory)
{
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

    internal async Task<CreateResult> CreateAsync(UuidV7 id, SamlServiceProviderDso.V1 dso, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(SamlServiceProviderEntityIdDskV1.Create(dso.EntityId))],
            BuildSearchFields(dso),
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<(SamlServiceProviderDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(SamlServiceProviderDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? ((SamlServiceProviderDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<(SamlServiceProviderDso.V1 Dso, int Version)?> TryReadByEntityIdAsync(string entityId, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            SamlServiceProviderDso.EntityType,
            DataStorageKey.Create(SamlServiceProviderEntityIdDskV1.Create(entityId)),
            ct);
        return result.Found ? ((SamlServiceProviderDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 id, SamlServiceProviderDso.V1 dso, int expectedVersion, Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [DataStorageKey.Create(SamlServiceProviderEntityIdDskV1.Create(dso.EntityId))],
            BuildSearchFields(dso),
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(SamlServiceProviderDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<QueryResult<SamlServiceProviderDso.V1>> QueryAsync(
        QueryRequest<SamlServiceProviderFilter, SamlServiceProviderSortField> request,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filter = BuildFilter(request.Filter?.FilterValue);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await store.QueryAsync<SamlServiceProviderDso.V1>(
            SamlServiceProviderDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => e.Value);
    }

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
