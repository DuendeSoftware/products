// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>
/// Filter criteria for role queries.
/// </summary>
public sealed record RoleFilter
{
    /// <summary>
    /// Filter by role name (contains match).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Filter by role description (contains match).
    /// </summary>
    public string? Description { get; init; }
}
