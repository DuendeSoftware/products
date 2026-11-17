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

    /// <summary>
    /// What happens to links of this type when the right-side entity is deleted. Defaults to
    /// <see cref="LinkDeleteBehavior.Cascade"/>.
    /// </summary>
    /// <remarks>
    /// This value is written into each link row's <c>restrict_delete</c> column when the row is inserted.
    /// Changing it for a link type that already has rows does not update those rows; Storage does not do
    /// this automatically. Update them yourself, for example
    /// <c>UPDATE entity_links SET restrict_delete = &lt;1|0, true|false, or the engine's boolean literal&gt;
    /// WHERE link_type_id = &lt;id&gt;</c>, run against each pool/database that has rows for that link type.
    /// </remarks>
    public LinkDeleteBehavior OnRightDelete { get; init; } = LinkDeleteBehavior.Cascade;
}
