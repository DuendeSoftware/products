// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Admin;

namespace Duende.UserManagement.Membership;

/// <summary>
/// Provides administrative operations for managing membership and role/group assignment,
/// independent of user profiles.
/// </summary>
public interface IMembershipAdmin
{
    /// <summary>
    /// Assigns a role directly to a membership. Auto-creates the membership if it does not exist.
    /// Idempotent — succeeds if already assigned.
    /// </summary>
    Task<SaveResult<RoleId>> AssignRoleAsync(UserSubjectId subjectId, RoleId roleId, Ct ct);

    /// <summary>
    /// Removes a direct role assignment from a membership. Idempotent — succeeds if not assigned.
    /// </summary>
    Task<SaveResult<RoleId>> RemoveRoleAsync(UserSubjectId subjectId, RoleId roleId, Ct ct);

    /// <summary>
    /// Assigns a role to a group. Idempotent — succeeds if already assigned.
    /// </summary>
    Task<SaveResult<RoleId>> AssignRoleToGroupAsync(RoleId roleId, GroupId groupId, Ct ct);

    /// <summary>
    /// Removes a role assignment from a group. Idempotent — succeeds if not assigned.
    /// </summary>
    Task<SaveResult<RoleId>> RemoveRoleFromGroupAsync(RoleId roleId, GroupId groupId, Ct ct);

    /// <summary>
    /// Adds a membership to a group. Auto-creates the membership if it does not exist.
    /// Idempotent — succeeds if already a member.
    /// </summary>
    Task<SaveResult<GroupId>> AssignGroupAsync(UserSubjectId subjectId, GroupId groupId, Ct ct);

    /// <summary>
    /// Removes a membership from a group. Idempotent — succeeds if not a member.
    /// </summary>
    Task<SaveResult<GroupId>> RemoveGroupAsync(UserSubjectId subjectId, GroupId groupId, Ct ct);

    /// <summary>
    /// Gets roles directly assigned to a membership.
    /// </summary>
    Task<QueryResult<RoleListItem>> GetDirectRolesAsync(UserSubjectId subjectId, DataRange? range, Ct ct);

    /// <summary>
    /// Gets roles transitively assigned to a membership via group membership.
    /// Uses a multi-hop link query: Role ← GroupRole ← Group ← MembershipGroup ← Membership.
    /// </summary>
    Task<QueryResult<RoleListItem>> GetTransitiveRolesAsync(UserSubjectId subjectId, DataRange? range, Ct ct);

    /// <summary>
    /// Gets roles assigned to a group.
    /// </summary>
    Task<QueryResult<RoleListItem>> GetRolesForGroupAsync(GroupId groupId, DataRange? range, Ct ct);

    /// <summary>
    /// Gets groups that a membership belongs to.
    /// </summary>
    Task<QueryResult<GroupListItem>> GetGroupsAsync(UserSubjectId subjectId, DataRange? range, Ct ct);

    /// <summary>
    /// Gets memberships directly assigned to a role.
    /// </summary>
    Task<QueryResult<MembershipRoleMemberListItem>> GetMembersInRoleAsync(RoleId roleId, DataRange? range, Ct ct);

    /// <summary>
    /// Gets groups that are assigned to a role.
    /// </summary>
    Task<QueryResult<RoleGroupMemberListItem>> GetGroupsInRoleAsync(RoleId roleId, DataRange? range, Ct ct);

    /// <summary>
    /// Gets memberships that are members of a group.
    /// </summary>
    Task<QueryResult<MembershipGroupMemberListItem>> GetMembersInGroupAsync(GroupId groupId, DataRange? range, Ct ct);
}
