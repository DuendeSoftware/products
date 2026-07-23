// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Represents a resolved, read-only view of a space.
/// Returned by <see cref="ISpaceStore"/> for runtime routing decisions.
/// </summary>
public sealed record Space
{
    /// <summary>Gets the unique identifier for this space.</summary>
    public required SpaceId Id { get; init; }

    /// <summary>Gets the display name for this space.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether this space is enabled.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Gets the match patterns used to route requests to this space.</summary>
    public required IReadOnlyList<SpaceMatchPattern> MatchPatterns { get; init; }

    /// <summary>Gets the pool identifier assigned to this space.</summary>
    public required PoolId PoolId { get; init; }
}
