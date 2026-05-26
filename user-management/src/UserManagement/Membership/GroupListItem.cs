// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>
/// List DTO for Group query results.
/// </summary>
public sealed record GroupListItem
{
    /// <summary>
    /// The unique identifier of the group.
    /// </summary>
    public required GroupId Id { get; init; }

    /// <summary>
    /// The name of the group.
    /// </summary>
    public required GroupName Name { get; init; }

    /// <summary>
    /// An optional description of the group.
    /// </summary>
    public GroupDescription? Description { get; init; }
}
