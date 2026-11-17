// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Static entry point for building link-exists query filter expressions — "an entity linked through this
/// definition exists (and optionally matches an inner filter)".
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class LinkFilter
{
    /// <summary>
    /// Creates a filter expression that matches entities with at least one link of <paramref name="definition"/>
    /// in the given <paramref name="direction"/>, optionally matching <paramref name="linkedFilter"/>.
    /// </summary>
    /// <param name="definition">The link definition describing the relationship to traverse.</param>
    /// <param name="direction">The direction to traverse, from the queried entity's point of view.</param>
    /// <param name="linkedFilter">
    /// An optional filter applied to the linked entity. <see langword="null"/> means "has any such link".
    /// Wrap the result with <see cref="Query.Not(IQueryFilterExpression)"/> to mean "has no such link".
    /// </param>
    public static IQueryFilterExpression Exists(LinkDefinition definition, LinkJoinDirection direction, IQueryFilterExpression? linkedFilter = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new LinkExistsExpression(definition, direction, linkedFilter);
    }

    /// <summary>
    /// Creates a filter expression that matches entities with at least one link to <paramref name="definition"/>'s
    /// right-side entity (the queried entity is the left side), optionally matching <paramref name="linkedFilter"/>.
    /// </summary>
    public static IQueryFilterExpression ToRight(LinkDefinition definition, IQueryFilterExpression? linkedFilter = null) =>
        Exists(definition, LinkJoinDirection.LeftToRight, linkedFilter);

    /// <summary>
    /// Creates a filter expression that matches entities with at least one link to <paramref name="definition"/>'s
    /// left-side entity (the queried entity is the right side), optionally matching <paramref name="linkedFilter"/>.
    /// </summary>
    public static IQueryFilterExpression ToLeft(LinkDefinition definition, IQueryFilterExpression? linkedFilter = null) =>
        Exists(definition, LinkJoinDirection.RightToLeft, linkedFilter);
}
