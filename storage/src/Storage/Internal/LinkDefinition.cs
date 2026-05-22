// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Defines the schema for a link type — binding a <see cref="LinkType"/> with its left and right <see cref="EntityType"/>s.
/// Define link definitions once as static instances and reference them everywhere.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record LinkDefinition
{
    /// <summary>The entity type on the left side of the link.</summary>
    public required EntityType Left { get; init; }

    /// <summary>The entity type on the right side of the link.</summary>
    public required EntityType Right { get; init; }

    /// <summary>The link type identifying this relationship.</summary>
    public required LinkType Link { get; init; }
}
