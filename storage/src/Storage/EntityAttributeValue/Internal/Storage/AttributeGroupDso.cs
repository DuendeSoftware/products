// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Provides the persisted data storage object representation of an attribute group.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal static class AttributeGroupDso
{
    /// <summary>
    /// Version 1 of the attribute group data storage object.
    /// </summary>
    /// <param name="Code">The group code.</param>
    /// <param name="DisplayName">The display name, or null.</param>
    /// <param name="Description">The description, or null.</param>
    /// <param name="Order">The sort order.</param>
    public sealed record V1(string Code, string? DisplayName, string? Description, int Order);
}
