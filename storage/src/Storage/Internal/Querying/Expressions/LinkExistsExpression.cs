// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks whether an entity linked through <paramref name="Definition"/> exists, from the
/// point of view of the queried entity, optionally matching <paramref name="LinkedFilter"/>.
/// </summary>
/// <param name="Definition">The link definition describing the relationship to traverse.</param>
/// <param name="Direction">
/// The direction to traverse, from the queried entity's point of view: <see cref="LinkJoinDirection.LeftToRight"/>
/// means the queried entity is <see cref="LinkDefinition.Left"/> and the condition applies to the
/// <see cref="LinkDefinition.Right"/> entity; <see cref="LinkJoinDirection.RightToLeft"/> is the mirror.
/// </param>
/// <param name="LinkedFilter">
/// An optional filter applied to the linked entity. <see langword="null"/> means "has any such link".
/// Use <see cref="Query.Not(IQueryFilterExpression)"/> around this expression to mean "has no such link".
/// </param>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record LinkExistsExpression(
    LinkDefinition Definition,
    LinkJoinDirection Direction,
    IQueryFilterExpression? LinkedFilter) : IQueryFilterExpression;
