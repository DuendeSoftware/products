// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Frozen;

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
    UuidV7? WhereEntityId)
{
    /// <summary>
    /// The set of terminal entities to read links for, set by <c>LinkQueryBuilder.WhereAny</c>.
    /// Only <c>IPartitionedStorage.QueryLinksByAnchorAsync</c> and <c>IPartitionedStorage.CountLinksAsync</c>
    /// accept a descriptor with this set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The set has more than <see cref="LinkQueryBuilder.MaxWhereAnyEntityIds"/> entries.
    /// </exception>
    public IReadOnlySet<UuidV7>? WhereEntityIds
    {
        get;
        init
        {
            var snapshot = value?.ToFrozenSet();
            if (snapshot is { Count: > LinkQueryBuilder.MaxWhereAnyEntityIds })
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    snapshot.Count,
                    $"WhereEntityIds accepts at most {LinkQueryBuilder.MaxWhereAnyEntityIds} entity ids.");
            }

            field = snapshot;
        }
    }
}

/// <summary>
/// A source entity paired with the <c>WhereAny</c> entity it was reached from.
/// </summary>
/// <param name="AnchorId">The <c>WhereAny</c> entity the result was reached from.</param>
/// <param name="Entity">The source entity reached from the anchor.</param>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record LinkedEnvelope(UuidV7 AnchorId, MetadataEnvelope Entity);

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
