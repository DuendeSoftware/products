// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Fully-qualified (schema + identifier-quoted) table names <see cref="SqlWhereClauseBuilder"/> needs to compile
/// a <see cref="Expressions.LinkExistsExpression"/> to a correlated <c>EXISTS</c> subquery. Engines pass these in
/// rather than the builder guessing quoting per engine.
/// </summary>
/// <param name="Entities">The qualified, quoted name of the <c>entities</c> table.</param>
/// <param name="EntityLinks">The qualified, quoted name of the <c>entity_links</c> table.</param>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record StorageTableNames(string Entities, string EntityLinks);
