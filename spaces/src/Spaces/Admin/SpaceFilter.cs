// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Spaces;

/// <summary>
/// Filter criteria for space queries.
/// </summary>
public sealed record SpaceFilter
{
    /// <summary>Filter by space name (contains match).</summary>
    public string? Name { get; init; }

    /// <summary>Filter by enabled status.</summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Filter by deletion state. Null includes all spaces regardless of deletion state,
    /// true returns only logically deleted spaces, false excludes deleted spaces.
    /// </summary>
    public bool? IsDeleted { get; init; }
}
