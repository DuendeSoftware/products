// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Admin;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Membership.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class MembershipAdmin(
    IStoreFactory storeFactory,
    MembershipRepository membershipRepo,
    RoleRepository roleRepo,
    GroupRepository groupRepo,
    ILogger<MembershipAdmin> logger,
    UserManagementLicenseValidator licenseValidator)
    : IMembershipAdmin
{
    private static readonly DataRange DefaultRange = DataRange.FromPage(1, 200);

    public async Task<SaveResult<RoleId>> AssignRoleAsync(UserSubjectId subjectId, RoleId roleId, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        using var scope = logger.BeginSubjectScope(subjectId);
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            logger.AssignRoleNotFound(LogLevel.Information, roleId, subjectId);
            return AdminError.NotFound(nameof(Role), roleId.ToString());
        }

        var userUuid = await membershipRepo.GetOrCreateUserUuidAsync(subjectId, ct);
        var store = storeFactory.GetStore();
        _ = await store.LinkAsync(MembershipLinkDefinitions.MembershipRole, userUuid, roleResult.Value.Role.StoreId, [], ct);

        logger.AssignRoleSucceeded(LogLevel.Information, roleId, subjectId);
        return SaveResult.Success(roleId, 0);
    }

    public async Task<SaveResult<RoleId>> RemoveRoleAsync(UserSubjectId subjectId, RoleId roleId, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        using var scope = logger.BeginSubjectScope(subjectId);
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            logger.RemoveRoleNotFound(LogLevel.Information, roleId, subjectId);
            return AdminError.NotFound(nameof(Role), roleId.ToString());
        }

        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            logger.RemoveRoleSucceeded(LogLevel.Information, roleId, subjectId);
            return SaveResult.Success(roleId, 0);
        }

        var store = storeFactory.GetStore();
        _ = await store.UnlinkAsync(MembershipLinkDefinitions.MembershipRole, existing.Resolved[subjectId], roleResult.Value.Role.StoreId, [], ct);

        logger.RemoveRoleSucceeded(LogLevel.Information, roleId, subjectId);
        return SaveResult.Success(roleId, 0);
    }

    public async Task<SaveResult<RoleId>> AssignRoleToGroupAsync(RoleId roleId, GroupId groupId, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            logger.AssignRoleToGroupRoleNotFound(LogLevel.Information, roleId, groupId);
            return AdminError.NotFound(nameof(Role), roleId.ToString());
        }

        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            logger.AssignRoleToGroupGroupNotFound(LogLevel.Information, roleId, groupId);
            return AdminError.NotFound(nameof(Group), groupId.ToString());
        }

        var store = storeFactory.GetStore();
        _ = await store.LinkAsync(MembershipLinkDefinitions.GroupRole, groupResult.Value.Group.StoreId, roleResult.Value.Role.StoreId, [], ct);

        logger.AssignRoleToGroupSucceeded(LogLevel.Information, roleId, groupId);
        return SaveResult.Success(roleId, 0);
    }

    public async Task<SaveResult<RoleId>> RemoveRoleFromGroupAsync(RoleId roleId, GroupId groupId, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            logger.RemoveRoleFromGroupRoleNotFound(LogLevel.Information, roleId, groupId);
            return AdminError.NotFound(nameof(Role), roleId.ToString());
        }

        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            logger.RemoveRoleFromGroupGroupNotFound(LogLevel.Information, roleId, groupId);
            return AdminError.NotFound(nameof(Group), groupId.ToString());
        }

        var store = storeFactory.GetStore();
        _ = await store.UnlinkAsync(MembershipLinkDefinitions.GroupRole, groupResult.Value.Group.StoreId, roleResult.Value.Role.StoreId, [], ct);

        logger.RemoveRoleFromGroupSucceeded(LogLevel.Information, roleId, groupId);
        return SaveResult.Success(roleId, 0);
    }

    public async Task<SaveResult<GroupId>> AssignGroupAsync(UserSubjectId subjectId, GroupId groupId, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        using var scope = logger.BeginSubjectScope(subjectId);
        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            logger.AssignGroupNotFound(LogLevel.Information, groupId, subjectId);
            return AdminError.NotFound(nameof(Group), groupId.ToString());
        }

        var userUuid = await membershipRepo.GetOrCreateUserUuidAsync(subjectId, ct);
        var store = storeFactory.GetStore();
        _ = await store.LinkAsync(MembershipLinkDefinitions.MembershipGroup, userUuid, groupResult.Value.Group.StoreId, [], ct);

        logger.AssignGroupSucceeded(LogLevel.Information, groupId, subjectId);
        return SaveResult.Success(groupId, 0);
    }

    public async Task<SaveResult<GroupId>> RemoveGroupAsync(UserSubjectId subjectId, GroupId groupId, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        using var scope = logger.BeginSubjectScope(subjectId);
        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            logger.RemoveGroupNotFound(LogLevel.Information, groupId, subjectId);
            return AdminError.NotFound(nameof(Group), groupId.ToString());
        }

        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            logger.RemoveGroupSucceeded(LogLevel.Information, groupId, subjectId);
            return SaveResult.Success(groupId, 0);
        }

        var store = storeFactory.GetStore();
        _ = await store.UnlinkAsync(MembershipLinkDefinitions.MembershipGroup, existing.Resolved[subjectId], groupResult.Value.Group.StoreId, [], ct);

        logger.RemoveGroupSucceeded(LogLevel.Information, groupId, subjectId);
        return SaveResult.Success(groupId, 0);
    }

    public async Task<QueryResult<RoleListItem>> GetDirectRolesAsync(UserSubjectId subjectId, DataRange? range, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            return EmptyResult<RoleListItem>();
        }

        var queryStore = storeFactory.GetStore();

        var query = LinkQuery
            .From(RoleDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipRole)
            .Where(UserDso.EntityType, existing.Resolved[subjectId])
            .Build();

        var result = await queryStore.QueryLinksAsync<RoleDso.V1>(query, range ?? DefaultRange, ct);

        logger.MembershipQueryExecuted(LogLevel.Debug, subjectId);
        return ToRoleQueryResult(result);
    }

    public async Task<QueryResult<RoleListItem>> GetTransitiveRolesAsync(UserSubjectId subjectId, DataRange? range, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            return EmptyResult<RoleListItem>();
        }

        var queryStore = storeFactory.GetStore();

        // Multi-hop: Role ← GroupRole ← Group ← MembershipGroup ← User
        var query = LinkQuery
            .From(RoleDso.EntityType)
            .Join(MembershipLinkDefinitions.GroupRole)
            .Join(MembershipLinkDefinitions.MembershipGroup)
            .Where(UserDso.EntityType, existing.Resolved[subjectId])
            .Build();

        var result = await queryStore.QueryLinksAsync<RoleDso.V1>(query, range ?? DefaultRange, ct);

        logger.MembershipQueryExecuted(LogLevel.Debug, subjectId);
        return ToRoleQueryResult(result);
    }

    public async Task<QueryResult<RoleListItem>> GetRolesForGroupAsync(GroupId groupId, DataRange? range, Ct ct)
    {
        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            return EmptyResult<RoleListItem>();
        }

        var queryStore = storeFactory.GetStore();

        var query = LinkQuery
            .From(RoleDso.EntityType)
            .Join(MembershipLinkDefinitions.GroupRole)
            .Where(GroupDso.EntityType, groupResult.Value.Group.StoreId)
            .Build();

        var result = await queryStore.QueryLinksAsync<RoleDso.V1>(query, range ?? DefaultRange, ct);

        logger.MembershipGroupQueryExecuted(LogLevel.Debug, groupId);
        return ToRoleQueryResult(result);
    }

    public async Task<QueryResult<GroupListItem>> GetGroupsAsync(UserSubjectId subjectId, DataRange? range, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            return EmptyResult<GroupListItem>();
        }

        var queryStore = storeFactory.GetStore();

        var query = LinkQuery
            .From(GroupDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipGroup)
            .Where(UserDso.EntityType, existing.Resolved[subjectId])
            .Build();

        var result = await queryStore.QueryLinksAsync<GroupDso.V1>(query, range ?? DefaultRange, ct);

        logger.MembershipQueryExecuted(LogLevel.Debug, subjectId);
        return new QueryResult<GroupListItem>
        {
            Items = result.Items.Select(e => new GroupListItem
            {
                Id = GroupId.Load(e.Value.GroupId),
                Name = GroupName.Load(e.Value.Name),
                Description = e.Value.Description is null ? (GroupDescription?)null : GroupDescription.Load(e.Value.Description)
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        };
    }

    public async Task<QueryResult<MembershipRoleMemberListItem>> GetMembersInRoleAsync(RoleId roleId, DataRange? range, Ct ct)
    {
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            return EmptyResult<MembershipRoleMemberListItem>();
        }

        var queryStore = storeFactory.GetStore();

        var query = LinkQuery
            .From(UserDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipRole)
            .Where(RoleDso.EntityType, roleResult.Value.Role.StoreId)
            .Build();

        var result = await queryStore.QueryLinksAsync<UserDso.V1>(query, range ?? DefaultRange, ct);

        logger.MembershipRoleQueryExecuted(LogLevel.Debug, roleId);
        return new QueryResult<MembershipRoleMemberListItem>
        {
            Items = result.Items.Select(e => new MembershipRoleMemberListItem
            {
                SubjectId = UserSubjectId.Load(e.Value.SubjectId),
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        };
    }

    public async Task<QueryResult<RoleGroupMemberListItem>> GetGroupsInRoleAsync(RoleId roleId, DataRange? range, Ct ct)
    {
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            return EmptyResult<RoleGroupMemberListItem>();
        }

        var queryStore = storeFactory.GetStore();

        var query = LinkQuery
            .From(GroupDso.EntityType)
            .Join(MembershipLinkDefinitions.GroupRole)
            .Where(RoleDso.EntityType, roleResult.Value.Role.StoreId)
            .Build();

        var result = await queryStore.QueryLinksAsync<GroupDso.V1>(query, range ?? DefaultRange, ct);

        logger.MembershipRoleQueryExecuted(LogLevel.Debug, roleId);
        return new QueryResult<RoleGroupMemberListItem>
        {
            Items = result.Items.Select(e => new RoleGroupMemberListItem
            {
                Id = GroupId.Load(e.Value.GroupId),
                Name = GroupName.Load(e.Value.Name)
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        };
    }

    public async Task<QueryResult<MembershipGroupMemberListItem>> GetMembersInGroupAsync(GroupId groupId, DataRange? range, Ct ct)
    {
        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            return EmptyResult<MembershipGroupMemberListItem>();
        }

        var queryStore = storeFactory.GetStore();

        var query = LinkQuery
            .From(UserDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipGroup)
            .Where(GroupDso.EntityType, groupResult.Value.Group.StoreId)
            .Build();

        var result = await queryStore.QueryLinksAsync<UserDso.V1>(query, range ?? DefaultRange, ct);

        logger.MembershipGroupQueryExecuted(LogLevel.Debug, groupId);
        return new QueryResult<MembershipGroupMemberListItem>
        {
            Items = result.Items.Select(e => new MembershipGroupMemberListItem
            {
                SubjectId = UserSubjectId.Load(e.Value.SubjectId),
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        };
    }

    private static QueryResult<RoleListItem> ToRoleQueryResult(QueryResult<MetadataEnvelope<RoleDso.V1>> result) =>
        new()
        {
            Items = result.Items.Select(e => new RoleListItem
            {
                Id = RoleId.Load(e.Value.RoleId),
                Name = RoleName.Load(e.Value.Name),
                Description = e.Value.Description is not null ? RoleDescription.Load(e.Value.Description) : (RoleDescription?)null
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        };

    private static QueryResult<T> EmptyResult<T>() =>
        new() { Items = [], TotalCount = 0, TotalPages = 0, HasMoreData = false };
}
