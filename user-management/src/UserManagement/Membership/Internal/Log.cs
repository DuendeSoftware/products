// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Membership.Internal;

internal static partial class Log
{
    // Group - Create
    [LoggerMessage(Message = $"Group create failed: duplicate name '{{{Parameters.GroupName}}}'")]
    internal static partial void GroupCreateDuplicateName(this ILogger logger, LogLevel level, string groupName);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} created successfully")]
    internal static partial void GroupCreateSucceeded(this ILogger logger, LogLevel level, GroupId groupId);

    // Group - Get / Not Found
    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} not found")]
    internal static partial void GroupNotFound(this ILogger logger, LogLevel level, GroupId groupId);

    // Group - Update
    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} update failed: version conflict")]
    internal static partial void GroupUpdateVersionConflict(this ILogger logger, LogLevel level, GroupId groupId);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} updated successfully")]
    internal static partial void GroupUpdateSucceeded(this ILogger logger, LogLevel level, GroupId groupId);

    // Group - Delete
    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} delete failed: not found")]
    internal static partial void GroupDeleteNotFound(this ILogger logger, LogLevel level, GroupId groupId);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} deleted successfully")]
    internal static partial void GroupDeleteSucceeded(this ILogger logger, LogLevel level, GroupId groupId);

    // Group - Query
    [LoggerMessage(Message = "Group query executed")]
    internal static partial void GroupQueryExecuted(this ILogger logger, LogLevel level);

    // Role - Create
    [LoggerMessage(Message = $"Role create failed: duplicate name '{{{Parameters.RoleName}}}'")]
    internal static partial void RoleCreateDuplicateName(this ILogger logger, LogLevel level, string roleName);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} created successfully")]
    internal static partial void RoleCreateSucceeded(this ILogger logger, LogLevel level, RoleId roleId);

    // Role - Get / Not Found
    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} not found")]
    internal static partial void RoleNotFound(this ILogger logger, LogLevel level, RoleId roleId);

    // Role - Update
    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} update failed: version conflict")]
    internal static partial void RoleUpdateVersionConflict(this ILogger logger, LogLevel level, RoleId roleId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} updated successfully")]
    internal static partial void RoleUpdateSucceeded(this ILogger logger, LogLevel level, RoleId roleId);

    // Role - Delete
    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} delete failed: not found")]
    internal static partial void RoleDeleteNotFound(this ILogger logger, LogLevel level, RoleId roleId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} deleted successfully")]
    internal static partial void RoleDeleteSucceeded(this ILogger logger, LogLevel level, RoleId roleId);

    // Role - Query
    [LoggerMessage(Message = "Role query executed")]
    internal static partial void RoleQueryExecuted(this ILogger logger, LogLevel level);

    // Membership - Role assignment
    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} not found when assigning to subject {{{LogParameters.SubjectId}}}")]
    internal static partial void AssignRoleNotFound(this ILogger logger, LogLevel level, RoleId roleId, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} assigned to subject {{{LogParameters.SubjectId}}}")]
    internal static partial void AssignRoleSucceeded(this ILogger logger, LogLevel level, RoleId roleId, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} not found when removing from subject {{{LogParameters.SubjectId}}}")]
    internal static partial void RemoveRoleNotFound(this ILogger logger, LogLevel level, RoleId roleId, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} removed from subject {{{LogParameters.SubjectId}}}")]
    internal static partial void RemoveRoleSucceeded(this ILogger logger, LogLevel level, RoleId roleId, UserSubjectId subjectId);

    // Membership - Group assignment
    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} not found when assigning to subject {{{LogParameters.SubjectId}}}")]
    internal static partial void AssignGroupNotFound(this ILogger logger, LogLevel level, GroupId groupId, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} assigned to subject {{{LogParameters.SubjectId}}}")]
    internal static partial void AssignGroupSucceeded(this ILogger logger, LogLevel level, GroupId groupId, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} not found when removing from subject {{{LogParameters.SubjectId}}}")]
    internal static partial void RemoveGroupNotFound(this ILogger logger, LogLevel level, GroupId groupId, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} removed from subject {{{LogParameters.SubjectId}}}")]
    internal static partial void RemoveGroupSucceeded(this ILogger logger, LogLevel level, GroupId groupId, UserSubjectId subjectId);

    // Membership - Role/Group on group
    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} not found when assigning to group {{{LogParameters.GroupId}}}")]
    internal static partial void AssignRoleToGroupRoleNotFound(this ILogger logger, LogLevel level, RoleId roleId, GroupId groupId);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} not found when assigning role {{{LogParameters.RoleId}}}")]
    internal static partial void AssignRoleToGroupGroupNotFound(this ILogger logger, LogLevel level, RoleId roleId, GroupId groupId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} assigned to group {{{LogParameters.GroupId}}}")]
    internal static partial void AssignRoleToGroupSucceeded(this ILogger logger, LogLevel level, RoleId roleId, GroupId groupId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} not found when removing from group {{{LogParameters.GroupId}}}")]
    internal static partial void RemoveRoleFromGroupRoleNotFound(this ILogger logger, LogLevel level, RoleId roleId, GroupId groupId);

    [LoggerMessage(Message = $"Group {{{LogParameters.GroupId}}} not found when removing role {{{LogParameters.RoleId}}}")]
    internal static partial void RemoveRoleFromGroupGroupNotFound(this ILogger logger, LogLevel level, RoleId roleId, GroupId groupId);

    [LoggerMessage(Message = $"Role {{{LogParameters.RoleId}}} removed from group {{{LogParameters.GroupId}}}")]
    internal static partial void RemoveRoleFromGroupSucceeded(this ILogger logger, LogLevel level, RoleId roleId, GroupId groupId);

    // Membership - Query
    [LoggerMessage(Message = $"Membership query executed for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void MembershipQueryExecuted(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Membership role query executed for role {{{LogParameters.RoleId}}}")]
    internal static partial void MembershipRoleQueryExecuted(this ILogger logger, LogLevel level, RoleId roleId);

    [LoggerMessage(Message = $"Membership group query executed for group {{{LogParameters.GroupId}}}")]
    internal static partial void MembershipGroupQueryExecuted(this ILogger logger, LogLevel level, GroupId groupId);

    private static class Parameters
    {
        internal const string GroupName = nameof(GroupName);
        internal const string RoleName = nameof(RoleName);
    }
}
