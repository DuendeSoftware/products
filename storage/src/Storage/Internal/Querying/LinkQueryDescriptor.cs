// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Describes a link traversal query — built by <c>LinkQueryBuilder</c> and
/// consumed by <c>IQueryStore.QueryLinks</c>.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record LinkQueryDescriptor(
    EntityType SourceEntityType,
    IReadOnlyList<LinkQueryJoin> Joins,
    EntityType? WhereEntityType,
    UuidV7? WhereEntityId);

/// <summary>
/// A single hop in a link query chain, pairing a <see cref="LinkDefinition"/>
/// with the direction it is traversed.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record LinkQueryJoin(
    LinkDefinition Definition,
    LinkJoinDirection Direction);

/// <summary>
/// Direction of a link traversal join.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum LinkJoinDirection
{
    /// <summary>Traverse from the left entity to the right entity.</summary>
    LeftToRight,

    /// <summary>Traverse from the right entity to the left entity.</summary>
    RightToLeft
}
