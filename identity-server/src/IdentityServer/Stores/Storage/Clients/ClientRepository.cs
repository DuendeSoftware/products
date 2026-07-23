// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.Clients;
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

namespace Duende.IdentityServer.Stores.Storage.Clients;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ClientRepository(IStoreFactory storeFactory)
{
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
        public static readonly StringArrayField AllowedScope = new("AllowedScope");
        public static readonly StringArrayField AllowedCorsOrigin = new("AllowedCorsOrigin");
    }

    internal async Task<CreateResult> CreateAsync(UuidV7 id, ClientDso.V1 dso, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(ClientIdDskV1.Create(dso.ClientId))],
            BuildSearchFields(dso),
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<(ClientDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(ClientDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? ((ClientDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<(ClientDso.V1 Dso, int Version)?> TryReadByClientIdAsync(string clientId, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            ClientDso.EntityType,
            DataStorageKey.Create(ClientIdDskV1.Create(clientId)),
            ct);
        return result.Found ? ((ClientDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 id, ClientDso.V1 dso, int expectedVersion, Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [DataStorageKey.Create(ClientIdDskV1.Create(dso.ClientId))],
            BuildSearchFields(dso),
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(ClientDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<bool> HasClientWithCorsOriginAsync(string origin, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filter = Fields.AllowedCorsOrigin.Contains(origin);
        var count = await store.CountAsync(ClientDso.EntityType, filter, ct);
        return count > 0;
    }

    internal async Task<QueryResult<ClientDso.V1>> QueryAsync(
        QueryRequest<ClientFilter, ClientSortField> request,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filter = BuildFilter(request.Filter?.FilterValue);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await store.QueryAsync<ClientDso.V1>(
            ClientDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => e.Value);
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

        var scopeIndex = 0;
        foreach (var scope in dso.AllowedScopes)
        {
            _ = builder.Add(Fields.AllowedScope.Path, scopeIndex++, scope);
        }

        var corsOriginIndex = 0;
        foreach (var origin in dso.AllowedCorsOrigins)
        {
            _ = builder.Add(Fields.AllowedCorsOrigin.Path, corsOriginIndex++, origin);
        }

        return builder.Build();
    }

    private static IQueryExpression BuildFilter(ClientFilter? filter)
    {
        if (filter is null)
        {
            return AllExpression.Instance;
        }

        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            expressions.Add(Fields.ClientId.Contains(filter.ClientId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientName))
        {
            expressions.Add(Fields.ClientName.Contains(filter.ClientName));
        }

        if (filter.Enabled.HasValue)
        {
            expressions.Add(Fields.Enabled.Equals(filter.Enabled.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.GrantType))
        {
            expressions.Add(Fields.GrantType.Contains(filter.GrantType));
        }

        if (!string.IsNullOrWhiteSpace(filter.AllowedScope))
        {
            expressions.Add(Fields.AllowedScope.Contains(filter.AllowedScope));
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
}
