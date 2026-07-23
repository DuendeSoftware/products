// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Filter criteria for space queries.
/// </summary>
public sealed record SpaceFilter
{
    /// <summary>Filter by space name (contains match).</summary>
    public string? Name { get; init; }

    /// <summary>Filter by enabled status.</summary>
    public bool? Enabled { get; init; }
}
