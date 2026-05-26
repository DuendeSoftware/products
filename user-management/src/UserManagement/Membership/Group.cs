// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>
/// Data transfer object for Group admin operations.
/// </summary>
public sealed record Group
{
    /// <summary>
    /// The name of the group.
    /// </summary>
    public required GroupName Name { get; init; }

    /// <summary>
    /// An optional description of the group.
    /// </summary>
    public GroupDescription? Description { get; init; }
}
