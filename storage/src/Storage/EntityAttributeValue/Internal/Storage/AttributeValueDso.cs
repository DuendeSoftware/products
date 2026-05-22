// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Provides the persisted data storage object representation of an attribute value.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal static class AttributeValueDso
{
    /// <summary>
    /// Version 1 of the attribute value data storage object.
    /// </summary>
    /// <param name="Name">The attribute name.</param>
    /// <param name="Value">The attribute value, or null.</param>
    public sealed record V1(string Name, object? Value);
}
