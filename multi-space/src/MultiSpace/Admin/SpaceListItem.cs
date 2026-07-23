// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Summary representation for list/query operations.
/// </summary>
public sealed record SpaceListItem
{
    /// <summary>Storage identifier.</summary>
    public required SpaceId Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the space is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>The pool identifier.</summary>
    public required PoolId PoolId { get; init; }

    /// <summary>Number of configured match patterns.</summary>
    public int MatchPatternCount { get; init; }
}
