// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Membership;

namespace Duende.UserManagement.Import;

/// <summary>
/// Group and role memberships to import for a user.
/// Referenced groups and roles must already exist — missing groups or roles
/// result in a hard failure on the individual record.
/// </summary>
public sealed record MembershipImport
{
    /// <summary>Groups to assign the user to.</summary>
    public IReadOnlyCollection<GroupId>? Groups { get; init; }

    /// <summary>Roles to assign directly to the user.</summary>
    public IReadOnlyCollection<RoleId>? DirectRoles { get; init; }
}
