// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// A registry of all link types stored in the system. Each link type must have a unique integer identifier.
///
/// Once a link type is assigned an identifier, it must never be changed or reused for a different link type.
/// </summary>
#pragma warning disable CA1008 // Enums should have zero value. We explicitly want to avoid assigning a zero value to any link type, to catch uninitialized values.
public enum LinkTypeRegistry
#pragma warning restore CA1008
{
    // user profile link types (1500-1599 range, aligned with entity types)
    GroupRole = 1502,

    MembershipRole = 1503,
    MembershipGroup = 1504,
}
