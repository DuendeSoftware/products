// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
using Duende.UserManagement.Internal.Storage;
using SearchFieldsBuilder = Duende.Storage.Internal.Querying.SearchFields.SearchFieldsBuilder;
using StorageSortDirection = Duende.Storage.Querying.SortDirection;

namespace Duende.UserManagement.Membership.Internal.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class RoleRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        RoleName = 1,
        RoleId = 2
    }

    private static class SearchFieldDefinitions
    {
        public static readonly StringField Name = new("Name");
        public static readonly StringField Description = new("Description");
    }

    // Create
    internal async Task<CreateResult> CreateAsync(Role role, Ct ct)
    {
        var store = storeFactory.GetStore();
        return await store.CreateAsync(
            role.StoreId,
            ToDso(role),
            GetKeys(role),
            GetSearchFields(role),
            Expiration.NoExpiration,
            [],
            ct);
    }

    // Read by RoleId (DSK lookup)
    internal async Task<(Role Role, int Version)?> TryReadAsync(RoleId id, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(
            RoleDso.EntityType,
            DataStorageKey.Create(RoleIdDskV1.Create(id)),
            ct);
        return result.Found
            ? (ToEntity(result.Dso, result.Id.Value), result.Version.Value)
            : null;
    }

    // Read by Name
    internal async Task<(Role Role, int Version)?> TryReadAsync(RoleName name, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(
            RoleDso.EntityType,
            DataStorageKey.Create(RoleNameDskV1.Create(name)),
            ct);
        return result.Found
            ? (ToEntity(result.Dso, result.Id.Value), result.Version.Value)
            : null;
    }

    // Update
    internal async Task<UpdateResult> UpdateAsync(Role role, int expectedVersion, Ct ct)
    {
        var store = storeFactory.GetStore();
        return await store.UpdateAsync(
            role.StoreId,
            ToDso(role),
            expectedVersion,
            GetKeys(role),
            GetSearchFields(role),
            expiration: null,
            [],
            ct);
    }

    // Delete by RoleId (DSK-based)
    internal async Task<DeleteResult> DeleteAsync(RoleId id, Ct ct)
    {
        var store = storeFactory.GetStore();
        return await store.DeleteAsync(
            RoleDso.EntityType,
            DataStorageKey.Create(RoleIdDskV1.Create(id)),
            [],
            ct);
    }

    // Query
    internal async Task<QueryResult<Role>> QueryAsync(
        RoleFilter? filter,
        SortBy.SortByField<RoleSortField>? sort,
        DataRange? range,
        Ct ct)
    {
        var queryStore = storeFactory.GetStore();
        var queryFilter = BuildFilter(filter);
        var sortParam = BuildSort(sort);
        var dataRange = range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await queryStore.QueryAsync<RoleDso.V1>(
            RoleDso.EntityType,
            queryFilter,
            sortParam,
            dataRange,
            ct);

        return result.ConvertTo(e => ToEntity(e.Value));
    }

    // Mapping: Entity to DSO
    private static RoleDso.V1 ToDso(Role entity) => new(
        Id: entity.StoreId.Value,
        RoleId: entity.Id.Value,
        Name: entity.Name.Value,
        Description: entity.Description?.Value);

    // Mapping: DSO to Entity (from query results where we only have the DSO)
    private static Role ToEntity(IDataStorageObject dso) =>
        dso switch
        {
            RoleDso.V1 v1 => ToEntity(v1),
            _ => throw new InvalidOperationException($"Unexpected DSO type: {dso.GetType().Name}")
        };

    // Mapping: DSO to Entity with known store ID (from get results)
    private static Role ToEntity(IDataStorageObject dso, Guid storeId) =>
        dso switch
        {
            RoleDso.V1 v1 => ToEntity(v1, storeId),
            _ => throw new InvalidOperationException($"Unexpected DSO type: {dso.GetType().Name}")
        };

    private static Role ToEntity(RoleDso.V1 dso) =>
        Role.Load(
            UuidV7.From(dso.Id),
            RoleId.Load(dso.RoleId),
            RoleName.Load(dso.Name),
            dso.Description is not null ? RoleDescription.Load(dso.Description) : (RoleDescription?)null);

    private static Role ToEntity(RoleDso.V1 dso, Guid storeId) =>
        Role.Load(
            UuidV7.From(storeId),
            RoleId.Load(dso.RoleId),
            RoleName.Load(dso.Name),
            dso.Description is not null ? RoleDescription.Load(dso.Description) : (RoleDescription?)null);

    // Storage helpers
    private static IReadOnlyList<DataStorageKey> GetKeys(Role entity) =>
    [
        DataStorageKey.Create(RoleNameDskV1.Create(entity.Name)),
        DataStorageKey.Create(RoleIdDskV1.Create(entity.Id))
    ];

    private static SearchFieldCollection GetSearchFields(Role entity)
    {
        var builder = new SearchFieldsBuilder()
            .Add(SearchFieldDefinitions.Name.Path, entity.Name.Value);

        if (entity.Description is { } description)
        {
            _ = builder.Add(SearchFieldDefinitions.Description.Path, description.Value);
        }

        return builder.Build();
    }

    // Query helpers
    private static IQueryExpression BuildFilter(RoleFilter? filter)
    {
        if (filter is null)
        {
            return AllExpression.Instance;
        }

        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            expressions.Add(SearchFieldDefinitions.Name.Contains(filter.Name));
        }

        if (!string.IsNullOrWhiteSpace(filter.Description))
        {
            expressions.Add(SearchFieldDefinitions.Description.Contains(filter.Description));
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

    private static SortParameter BuildSort(SortBy.SortByField<RoleSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(SearchFieldDefinitions.Name);
        }

        Field field = sort.Field switch
        {
            RoleSortField.Name => SearchFieldDefinitions.Name,
            RoleSortField.Description => SearchFieldDefinitions.Description,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, "Unknown sort field")
        };

        var direction = sort.Direction switch
        {
            StorageSortDirection.Ascending => StorageSortDirection.Ascending,
            StorageSortDirection.Descending => StorageSortDirection.Descending,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Direction, "Unknown sort direction")
        };

        return new SortParameter(field, direction);
    }
}
