// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// A registry of all link types stored in the system. Each link type must have a unique integer identifier.
///
/// Once a link type is assigned an identifier, it must never be changed or reused for a different link type.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
#pragma warning disable CA1008 // Enums should have zero value. We explicitly want to avoid assigning a zero value to any link type, to catch uninitialized values.
public enum LinkTypeRegistry
#pragma warning restore CA1008
{
    /// <summary>
    /// Link type for group-to-role relationships.
    /// </summary>
    // user profile link types (1500-1599 range, aligned with entity types)
    GroupRole = 1502,

    /// <summary>
    /// Link type for membership-to-role relationships.
    /// </summary>
    MembershipRole = 1503,

    /// <summary>
    /// Link type for membership-to-group relationships.
    /// </summary>
    MembershipGroup = 1504,
}
