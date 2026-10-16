// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal;

/// <summary>
/// Creates read-only attribute schemas backed by the storage schema implementation.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption,
/// and is not subject to semantic versioning rules.
/// </remarks>
public static class AttributeSchemaFactory
{
    /// <summary>
    /// Gets a schema with no attribute definitions and no groups.
    /// </summary>
    public static IReadOnlyAttributeSchema Empty => AttributeSchema.Empty;

    /// <summary>
    /// Creates a read-only schema from the given attribute definitions, without groups.
    /// </summary>
    /// <param name="attributes">The attribute definitions. For duplicate codes, the last definition wins.</param>
    /// <returns>The schema.</returns>
    public static IReadOnlyAttributeSchema Create(IEnumerable<AttributeDefinition> attributes) =>
        AttributeSchema.Load(attributes);

    /// <summary>
    /// Creates a read-only schema from the given attribute definitions and groups.
    /// </summary>
    /// <param name="attributes">The attribute definitions. For duplicate codes, the last definition wins.</param>
    /// <param name="groups">The attribute groups. For duplicate codes, the last group wins.</param>
    /// <returns>The schema.</returns>
    public static IReadOnlyAttributeSchema Create(
        IEnumerable<AttributeDefinition> attributes,
        IEnumerable<AttributeGroup> groups) =>
        AttributeSchema.Load(attributes, groups);
}
