// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Represents the configuration needed to create a new space.
/// </summary>
public sealed record CreateSpaceConfiguration
{
    /// <summary>Gets the display name for the space.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the match patterns used to route requests to this space.</summary>
    public required IReadOnlyList<SpaceMatchPattern> MatchPatterns { get; init; }

    /// <summary>
    /// Gets the optional pool ID to assign to the space. When <c>null</c>, the pool ID is
    /// auto-assigned. When specified, the value must be greater than zero and not already in use.
    /// </summary>
    public PoolId? PoolId { get; init; }
}
