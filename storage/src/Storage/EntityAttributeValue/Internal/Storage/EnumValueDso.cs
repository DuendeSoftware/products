// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Persisted representation of an enumeration value within an attribute type.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="Key">The enum value key.</param>
/// <param name="DisplayName">The human-readable display name.</param>
internal sealed record EnumValueDso(string Key, string DisplayName);
