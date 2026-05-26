// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>
/// DTO for listing groups that are members of a role.
/// </summary>
public sealed record RoleGroupMemberListItem
{
    /// <summary>
    /// The unique identifier of the group.
    /// </summary>
    public required GroupId Id { get; init; }

    /// <summary>
    /// The name of the group.
    /// </summary>
    public required GroupName Name { get; init; }
}
