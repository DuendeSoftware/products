// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.Spaces;

/// <summary>
/// Represents the mutable configuration of a space, used for CRUD operations via <see cref="ISpaceAdmin"/>.
/// </summary>
public sealed class SpaceConfiguration
{
    /// <summary>Gets the unique identifier for this space.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets or sets the display name for this space.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets whether this space is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the match patterns used to route requests to this space.</summary>
    public required IReadOnlyList<SpaceMatchPattern> MatchPatterns { get; set; }

    /// <summary>Gets the pool identifier assigned to this space. Immutable after creation.</summary>
    public required PoolId PoolId { get; init; }

    /// <summary>Gets whether this space has been logically deleted.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Gets or sets the schema-validated extended properties for this space.
    /// Requires a space schema to be registered via <c>ISchemaStore</c>.
    /// </summary>
    public IReadOnlyCollection<AttributeValue> ExtendedProperties { get; set; } = [];

    /// <summary>
    /// Returns a read-only <see cref="Space"/> view of this configuration.
    /// </summary>
    internal Space ToSpace() =>
        new()
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            MatchPatterns = MatchPatterns,
            PoolId = PoolId,
            ExtendedProperties = ExtendedProperties
        };
}
