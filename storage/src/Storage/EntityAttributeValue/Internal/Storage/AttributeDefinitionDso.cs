// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Provides the persisted data storage object representation of an attribute definition.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal static class AttributeDefinitionDso
{
    /// <summary>
    /// Version 1 of the attribute definition data storage object.
    /// </summary>
    /// <param name="Code">The attribute code.</param>
    /// <param name="Type">The attribute type descriptor.</param>
    /// <param name="Description">The description, or null.</param>
    /// <param name="IsUnique">Whether the attribute value must be unique.</param>
    /// <param name="Tags">The tags associated with the attribute.</param>
    /// <param name="GroupCode">The group code, or null if ungrouped.</param>
    /// <param name="Order">The sort order.</param>
    /// <param name="DisplayName">The display name, or null.</param>
    /// <param name="IsQueryable">Whether the attribute can be used in queries.</param>
    /// <param name="IsRequired">Whether the attribute is required.</param>
    public sealed record V1(
        string Code,
        AttributeTypeDso Type,
        string? Description,
        bool IsUnique,
        IReadOnlyList<string> Tags,
        string? GroupCode,
        int Order,
        string? DisplayName,
        bool IsQueryable,
        bool IsRequired);
}
