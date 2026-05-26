// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Membership;

/// <summary>
/// Filter criteria for group queries.
/// </summary>
public sealed record GroupFilter
{
    /// <summary>
    /// Filter by group name (contains match).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Filter by group description (contains match).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// An optional SCIM-like search expression (e.g., <c>displayName eq "Engineers"</c>).
    /// When provided, translated into query filters using SCIM filter syntax (RFC 7644 §3.4.2.2).
    /// Combined with the other filter properties using AND logic when both are specified.
    /// </summary>
    public SearchExpression? SearchExpression { get; init; }
}

/// <summary>
/// Sort field options for group queries.
/// </summary>
public enum GroupSortField
{
    /// <summary>Sort by group name.</summary>
    Name,

    /// <summary>Sort by group description.</summary>
    Description
}
