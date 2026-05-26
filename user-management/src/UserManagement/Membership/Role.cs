// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>
/// Data transfer object for Role admin operations.
/// </summary>
public sealed record Role
{
    /// <summary>
    /// The name of the role.
    /// </summary>
    public required RoleName Name { get; init; }

    /// <summary>
    /// An optional description of the role.
    /// </summary>
    public RoleDescription? Description { get; init; }
}
