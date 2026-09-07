// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.Spaces;

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

    /// <summary>
    /// Gets the schema-validated extended properties for this space.
    /// Requires a space schema to be registered via <c>ISchemaStore</c>.
    /// </summary>
    public AttributeValueCollection ExtendedProperties { get; init; } = new();
}
