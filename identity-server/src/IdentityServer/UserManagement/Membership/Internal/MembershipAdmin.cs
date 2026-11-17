// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership.Internal.Storage;
using Microsoft.Extensions.Logging;
using IPartitionedStorageFactory = Duende.Storage.Internal.IPartitionedStorageFactory;
using DataCategoryName = Duende.Storage.DataCategoryName;

namespace Duende.UserManagement.Membership.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class MembershipAdmin(
    IPartitionedStorageFactory partitionedStorageFactory,
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
            return SaveResult.Failure<RoleId>(StorageError.NotFound(nameof(Role), roleId.ToString()));
        }

        var userUuid = await membershipRepo.GetOrCreateUserUuidAsync(subjectId, ct);
        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        _ = await partitionedStorage.LinkAsync(MembershipLinkDefinitions.MembershipRole, userUuid, roleResult.Value.Role.StoreId, [], ct);

        logger.AssignRoleSucceeded(LogLevel.Information, roleId, subjectId);
        return SaveResult.Success(roleId, (DataVersion)(0));
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
            return SaveResult.Failure<RoleId>(StorageError.NotFound(nameof(Role), roleId.ToString()));
        }

        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            logger.RemoveRoleSucceeded(LogLevel.Information, roleId, subjectId);
            return SaveResult.Success(roleId, (DataVersion)(0));
        }

        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        _ = await partitionedStorage.UnlinkAsync(MembershipLinkDefinitions.MembershipRole, existing.Resolved[subjectId], roleResult.Value.Role.StoreId, [], ct);

        logger.RemoveRoleSucceeded(LogLevel.Information, roleId, subjectId);
        return SaveResult.Success(roleId, (DataVersion)(0));
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
            return SaveResult.Failure<RoleId>(StorageError.NotFound(nameof(Role), roleId.ToString()));
        }

        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            logger.AssignRoleToGroupGroupNotFound(LogLevel.Information, roleId, groupId);
            return SaveResult.Failure<RoleId>(StorageError.NotFound(nameof(Group), groupId.ToString()));
        }

        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        _ = await partitionedStorage.LinkAsync(MembershipLinkDefinitions.GroupRole, groupResult.Value.Group.StoreId, roleResult.Value.Role.StoreId, [], ct);

        logger.AssignRoleToGroupSucceeded(LogLevel.Information, roleId, groupId);
        return SaveResult.Success(roleId, (DataVersion)(0));
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
            return SaveResult.Failure<RoleId>(StorageError.NotFound(nameof(Role), roleId.ToString()));
        }

        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            logger.RemoveRoleFromGroupGroupNotFound(LogLevel.Information, roleId, groupId);
            return SaveResult.Failure<RoleId>(StorageError.NotFound(nameof(Group), groupId.ToString()));
        }

        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        _ = await partitionedStorage.UnlinkAsync(MembershipLinkDefinitions.GroupRole, groupResult.Value.Group.StoreId, roleResult.Value.Role.StoreId, [], ct);

        logger.RemoveRoleFromGroupSucceeded(LogLevel.Information, roleId, groupId);
        return SaveResult.Success(roleId, (DataVersion)(0));
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
            return SaveResult.Failure<GroupId>(StorageError.NotFound(nameof(Group), groupId.ToString()));
        }

        var userUuid = await membershipRepo.GetOrCreateUserUuidAsync(subjectId, ct);
        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        _ = await partitionedStorage.LinkAsync(MembershipLinkDefinitions.MembershipGroup, userUuid, groupResult.Value.Group.StoreId, [], ct);

        logger.AssignGroupSucceeded(LogLevel.Information, groupId, subjectId);
        return SaveResult.Success(groupId, (DataVersion)(0));
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
            return SaveResult.Failure<GroupId>(StorageError.NotFound(nameof(Group), groupId.ToString()));
        }

        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            logger.RemoveGroupSucceeded(LogLevel.Information, groupId, subjectId);
            return SaveResult.Success(groupId, (DataVersion)(0));
        }

        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        _ = await partitionedStorage.UnlinkAsync(MembershipLinkDefinitions.MembershipGroup, existing.Resolved[subjectId], groupResult.Value.Group.StoreId, [], ct);

        logger.RemoveGroupSucceeded(LogLevel.Information, groupId, subjectId);
        return SaveResult.Success(groupId, (DataVersion)(0));
    }

    public async Task<QueryResult<RoleListItem>> GetDirectRolesAsync(UserSubjectId subjectId, DataRange? range, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            return EmptyResult<RoleListItem>();
        }

        var queryPartitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        var query = StorageQuerying.LinkQuery
            .From(RoleDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipRole)
            .Where(UserDso.EntityType, existing.Resolved[subjectId])
            .Build();

        var result = await queryPartitionedStorage.QueryLinksAsync(query, (range ?? DefaultRange).ToStorage()!, ct);

        logger.MembershipQueryExecuted(LogLevel.Debug, subjectId);
        return ToRoleQueryResult(result, range);
    }

    public async Task<QueryResult<RoleListItem>> GetTransitiveRolesAsync(UserSubjectId subjectId, DataRange? range, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            return EmptyResult<RoleListItem>();
        }

        var queryPartitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        // Multi-hop: Role ← GroupRole ← Group ← MembershipGroup ← User
        var query = StorageQuerying.LinkQuery
            .From(RoleDso.EntityType)
            .Join(MembershipLinkDefinitions.GroupRole)
            .Join(MembershipLinkDefinitions.MembershipGroup)
            .Where(UserDso.EntityType, existing.Resolved[subjectId])
            .Build();

        var result = await queryPartitionedStorage.QueryLinksAsync(query, (range ?? DefaultRange).ToStorage()!, ct);

        logger.MembershipQueryExecuted(LogLevel.Debug, subjectId);
        return ToRoleQueryResult(result, range);
    }

    public async Task<QueryResult<RoleListItem>> GetRolesForGroupAsync(GroupId groupId, DataRange? range, Ct ct)
    {
        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            return EmptyResult<RoleListItem>();
        }

        var queryPartitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        var query = StorageQuerying.LinkQuery
            .From(RoleDso.EntityType)
            .Join(MembershipLinkDefinitions.GroupRole)
            .Where(GroupDso.EntityType, groupResult.Value.Group.StoreId)
            .Build();

        var result = await queryPartitionedStorage.QueryLinksAsync(query, (range ?? DefaultRange).ToStorage()!, ct);

        logger.MembershipGroupQueryExecuted(LogLevel.Debug, groupId);
        return ToRoleQueryResult(result, range);
    }

    public async Task<QueryResult<GroupListItem>> GetGroupsAsync(UserSubjectId subjectId, DataRange? range, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        var existing = await membershipRepo.ResolveUserUuidsAsync([subjectId], ct);
        if (existing.Resolved.Count == 0)
        {
            return EmptyResult<GroupListItem>();
        }

        var queryPartitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        var query = StorageQuerying.LinkQuery
            .From(GroupDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipGroup)
            .Where(UserDso.EntityType, existing.Resolved[subjectId])
            .Build();

        var result = await queryPartitionedStorage.QueryLinksAsync(query, (range ?? DefaultRange).ToStorage()!, ct);

        logger.MembershipQueryExecuted(LogLevel.Debug, subjectId);
        return new StorageQuerying.QueryResult<GroupListItem>
        {
            Items = result.Items.Select(e =>
            {
                var dso = GroupDso.ToCurrent(e.Value);
                return new GroupListItem
                {
                    Id = GroupId.Load(dso.GroupId),
                    Name = GroupName.Load(dso.Name),
                    Description = dso.Description is null ? (GroupDescription?)null : GroupDescription.Load(dso.Description)
                };
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            HasPreviousData = result.HasPreviousData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        }.ToModel(range);
    }

    public async Task<QueryResult<MembershipRoleMemberListItem>> GetMembersInRoleAsync(RoleId roleId, DataRange? range, Ct ct)
    {
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            return EmptyResult<MembershipRoleMemberListItem>();
        }

        var queryPartitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        var query = StorageQuerying.LinkQuery
            .From(UserDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipRole)
            .Where(RoleDso.EntityType, roleResult.Value.Role.StoreId)
            .Build();

        var result = await queryPartitionedStorage.QueryLinksAsync(query, (range ?? DefaultRange).ToStorage()!, ct);

        logger.MembershipRoleQueryExecuted(LogLevel.Debug, roleId);
        return new StorageQuerying.QueryResult<MembershipRoleMemberListItem>
        {
            Items = result.Items.Select(e => new MembershipRoleMemberListItem
            {
                SubjectId = UserSubjectId.Load(UserDso.ToCurrent(e.Value).SubjectId),
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            HasPreviousData = result.HasPreviousData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        }.ToModel(range);
    }

    public async Task<QueryResult<RoleGroupMemberListItem>> GetGroupsInRoleAsync(RoleId roleId, DataRange? range, Ct ct)
    {
        var roleResult = await roleRepo.TryReadAsync(roleId, ct);
        if (!roleResult.HasValue)
        {
            return EmptyResult<RoleGroupMemberListItem>();
        }

        var queryPartitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        var query = StorageQuerying.LinkQuery
            .From(GroupDso.EntityType)
            .Join(MembershipLinkDefinitions.GroupRole)
            .Where(RoleDso.EntityType, roleResult.Value.Role.StoreId)
            .Build();

        var result = await queryPartitionedStorage.QueryLinksAsync(query, (range ?? DefaultRange).ToStorage()!, ct);

        logger.MembershipRoleQueryExecuted(LogLevel.Debug, roleId);
        return new StorageQuerying.QueryResult<RoleGroupMemberListItem>
        {
            Items = result.Items.Select(e =>
            {
                var dso = GroupDso.ToCurrent(e.Value);
                return new RoleGroupMemberListItem
                {
                    Id = GroupId.Load(dso.GroupId),
                    Name = GroupName.Load(dso.Name)
                };
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            HasPreviousData = result.HasPreviousData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        }.ToModel(range);
    }

    public async Task<QueryResult<MembershipGroupMemberListItem>> GetMembersInGroupAsync(GroupId groupId, DataRange? range, Ct ct)
    {
        var groupResult = await groupRepo.TryReadAsync(groupId, ct);
        if (!groupResult.HasValue)
        {
            return EmptyResult<MembershipGroupMemberListItem>();
        }

        var queryPartitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        var query = StorageQuerying.LinkQuery
            .From(UserDso.EntityType)
            .Join(MembershipLinkDefinitions.MembershipGroup)
            .Where(GroupDso.EntityType, groupResult.Value.Group.StoreId)
            .Build();

        var result = await queryPartitionedStorage.QueryLinksAsync(query, (range ?? DefaultRange).ToStorage()!, ct);

        logger.MembershipGroupQueryExecuted(LogLevel.Debug, groupId);
        return new StorageQuerying.QueryResult<MembershipGroupMemberListItem>
        {
            Items = result.Items.Select(e => new MembershipGroupMemberListItem
            {
                SubjectId = UserSubjectId.Load(UserDso.ToCurrent(e.Value).SubjectId),
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            HasPreviousData = result.HasPreviousData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        }.ToModel(range);
    }

    private static QueryResult<RoleListItem> ToRoleQueryResult(StorageQuerying.QueryResult<StorageQuerying.MetadataEnvelope> result, DataRange? range) =>
        new StorageQuerying.QueryResult<RoleListItem>
        {
            Items = result.Items.Select(e =>
            {
                var dso = RoleDso.ToCurrent(e.Value);
                return new RoleListItem
                {
                    Id = RoleId.Load(dso.RoleId),
                    Name = RoleName.Load(dso.Name),
                    Description = dso.Description is not null ? RoleDescription.Load(dso.Description) : (RoleDescription?)null
                };
            }).ToList(),
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasMoreData = result.HasMoreData,
            HasPreviousData = result.HasPreviousData,
            NextToken = result.NextToken,
            PreviousToken = result.PreviousToken
        }.ToModel(range);

    private static QueryResult<T> EmptyResult<T>() =>
        new() { Results = [], TotalCount = 0, TotalPages = 0, HasNextResults = false, HasPrevResults = false, ResultsToken = "" };
}
