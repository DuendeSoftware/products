// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>
/// List DTO for Role query results.
/// </summary>
public sealed record RoleListItem
{
    /// <summary>
    /// The unique identifier of the role.
    /// </summary>
    public required RoleId Id { get; init; }

    /// <summary>
    /// The name of the role.
    /// </summary>
    public required RoleName Name { get; init; }

    /// <summary>
    /// An optional description of the role.
    /// </summary>
    public RoleDescription? Description { get; init; }
}
