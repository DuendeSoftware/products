// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Filtering;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using QueryBuilder = Duende.Storage.Internal.Querying.Query;
using SearchFieldsBuilder = Duende.Storage.Internal.Querying.SearchFields.SearchFieldsBuilder;
using StorageSortDirection = Duende.Storage.Querying.SortDirection;

namespace Duende.UserManagement.Membership.Internal.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class GroupRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        GroupName = 1,
        GroupId = 2
    }

    private static class SearchFieldDefinitions
    {
        public static readonly StringField Name = new("Name");
        public static readonly StringField Description = new("Description");
    }

    internal async Task<CreateResult> CreateAsync(Group group, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            group.StoreId,
            ToDso(group),
            GetKeys(group),
            GetSearchFields(group),
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<(Group Group, int Version)?> TryReadAsync(GroupId id, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            GroupDso.EntityType,
            DataStorageKey.Create(GroupIdDskV1.Create(id)),
            ct);
        return result.Found
            ? (ToEntity(result.Dso, result.Id.Value), result.Version.Value)
            : null;
    }

    internal async Task<(Group Group, int Version)?> TryReadAsync(GroupName name, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            GroupDso.EntityType,
            DataStorageKey.Create(GroupNameDskV1.Create(name)),
            ct);
        return result.Found
            ? (ToEntity(result.Dso, result.Id.Value), result.Version.Value)
            : null;
    }

    internal async Task<UpdateResult> UpdateAsync(Group group, int expectedVersion, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.UpdateAsync(
            group.StoreId,
            ToDso(group),
            expectedVersion,
            GetKeys(group),
            GetSearchFields(group),
            expiration: null,
            [],
            ct);
    }

    internal async Task<DeleteResult> DeleteAsync(GroupId id, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.DeleteAsync(
            GroupDso.EntityType,
            DataStorageKey.Create(GroupIdDskV1.Create(id)),
            [],
            ct);
    }

    /// <summary>
    /// Creates a group and establishes member links atomically in a single batch.
    /// The <paramref name="userUuids"/> must be pre-resolved UserDso UUIDs.
    /// </summary>
    internal async Task<BatchResult> CreateWithMembersAsync(
        Group group,
        IReadOnlyList<UuidV7> userUuids,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var groupUuid = group.StoreId;

        var ops = new List<IStoreOperation>
        {
            CreateOperation.For(groupUuid, ToDso(group), GetKeys(group), GetSearchFields(group), Expiration.NoExpiration)
        };

        foreach (var userUuid in userUuids)
        {
            ops.Add(LinkOperation.For(MembershipLinkDefinitions.MembershipGroup, userUuid, groupUuid));
        }

        return await store.ExecuteBatchAsync(ops, [], ct);
    }

    /// <summary>
    /// Updates a group and applies membership changes atomically in a single batch.
    /// Pass empty collections for <paramref name="linksToAdd"/> / <paramref name="linksToRemove"/>
    /// when only the entity or only the links need to change.
    /// </summary>
    internal async Task<BatchResult> UpdateWithMembershipChangesAsync(
        Group group,
        int expectedVersion,
        IReadOnlyList<UuidV7> linksToAdd,
        IReadOnlyList<UuidV7> linksToRemove,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var groupUuid = group.StoreId;

        var ops = new List<IStoreOperation>
        {
            UpdateOperation.For(groupUuid, ToDso(group), expectedVersion, GetKeys(group), GetSearchFields(group), expiration: null)
        };

        foreach (var userUuid in linksToRemove)
        {
            ops.Add(UnlinkOperation.For(MembershipLinkDefinitions.MembershipGroup, userUuid, groupUuid));
        }

        foreach (var userUuid in linksToAdd)
        {
            ops.Add(LinkOperation.For(MembershipLinkDefinitions.MembershipGroup, userUuid, groupUuid));
        }

        return await store.ExecuteBatchAsync(ops, [], ct);
    }

    /// <summary>
    /// Applies membership link changes atomically without modifying the group entity.
    /// </summary>
    internal async Task<BatchResult> UpdateMembershipAsync(
        UuidV7 groupStoreId,
        IReadOnlyList<UuidV7> linksToAdd,
        IReadOnlyList<UuidV7> linksToRemove,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);

        var ops = new List<IStoreOperation>();

        foreach (var userUuid in linksToRemove)
        {
            ops.Add(UnlinkOperation.For(MembershipLinkDefinitions.MembershipGroup, userUuid, groupStoreId));
        }

        foreach (var userUuid in linksToAdd)
        {
            ops.Add(LinkOperation.For(MembershipLinkDefinitions.MembershipGroup, userUuid, groupStoreId));
        }

        if (ops.Count == 0)
        {
            return BatchResult.Successful(0);
        }

        return await store.ExecuteBatchAsync(ops, [], ct);
    }

    internal async Task<QueryResult<Group>> QueryAsync(
        GroupFilter? filter,
        SortBy.SortByField<GroupSortField>? sort,
        DataRange? range,
        Ct ct)
    {
        var queryStore = await storeFactory.GetStore(ct);
        var queryFilter = BuildFilter(filter);
        var sortParam = BuildSort(sort);
        var dataRange = range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await queryStore.QueryAsync<GroupDso.V1>(
            GroupDso.EntityType,
            queryFilter,
            sortParam,
            dataRange,
            ct);

        return result.ConvertTo(e => ToEntity(e.Value));
    }

    private static GroupDso.V1 ToDso(Group entity) => new(
        Id: entity.StoreId.Value,
        GroupId: entity.Id.Value,
        Name: entity.Name.Value,
        Description: entity.Description?.Value);

    private static Group ToEntity(IDataStorageObject dso) =>
        dso switch
        {
            GroupDso.V1 v1 => ToEntity(v1),
            _ => throw new InvalidOperationException($"Unexpected DSO type: {dso.GetType().Name}")
        };

    private static Group ToEntity(IDataStorageObject dso, Guid storeId) =>
        dso switch
        {
            GroupDso.V1 v1 => ToEntity(v1, storeId),
            _ => throw new InvalidOperationException($"Unexpected DSO type: {dso.GetType().Name}")
        };

    private static Group ToEntity(GroupDso.V1 dso) =>
        Group.Load(
            UuidV7.From(dso.Id),
            GroupId.Load(dso.GroupId),
            GroupName.Load(dso.Name),
            dso.Description is not null ? GroupDescription.Load(dso.Description) : (GroupDescription?)null);

    private static Group ToEntity(GroupDso.V1 dso, Guid storeId) =>
        Group.Load(
            UuidV7.From(storeId),
            GroupId.Load(dso.GroupId),
            GroupName.Load(dso.Name),
            dso.Description is not null ? GroupDescription.Load(dso.Description) : (GroupDescription?)null);

    private static IReadOnlyList<DataStorageKey> GetKeys(Group entity) =>
    [
        DataStorageKey.Create(GroupNameDskV1.Create(entity.Name)),
        DataStorageKey.Create(GroupIdDskV1.Create(entity.Id))
    ];

    private static SearchFieldCollection GetSearchFields(Group entity)
    {
        var builder = new SearchFieldsBuilder()
            .Add(SearchFieldDefinitions.Name.Path, entity.Name.Value);

        if (entity.Description is { } description)
        {
            _ = builder.Add(SearchFieldDefinitions.Description.Path, description.Value);
        }

        return builder.Build();
    }

    private static IQueryExpression BuildFilter(GroupFilter? filter)
    {
        var propertyFilter = BuildPropertyFilter(filter);
        var scimFilter = TranslateSearchExpression(filter?.SearchExpression);

        return (propertyFilter, scimFilter) switch
        {
            (null, null) => AllExpression.Instance,
            (not null, null) => propertyFilter,
            (null, not null) => QueryBuilder.Where(scimFilter),
            (not null, not null) => propertyFilter.And(scimFilter)
        };
    }

    private static IQueryFilterExpression? BuildPropertyFilter(GroupFilter? filter)
    {
        if (filter is null)
        {
            return null;
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
            return null;
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

    private static IQueryFilterExpression? TranslateSearchExpression(SearchExpression? searchExpression)
    {
        if (searchExpression is null)
        {
            return null;
        }

        var resolver = new GroupAttributeTypeResolver();
        var translator = new FilterTranslator(resolver);
        return translator.Translate(searchExpression.Value.ToString());
    }

    private static SortParameter BuildSort(SortBy.SortByField<GroupSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(SearchFieldDefinitions.Name);
        }

        Field field = sort.Field switch
        {
            GroupSortField.Name => SearchFieldDefinitions.Name,
            GroupSortField.Description => SearchFieldDefinitions.Description,
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
