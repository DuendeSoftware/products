// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that negates an inner filter expression.
/// Used for SCIM 'not' operator and 'ne' (as Not(Equal(...))).
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record NotExpression : IQueryFilterExpression
{
    /// <summary>
    /// The inner expression to negate.
    /// </summary>
    public IQueryFilterExpression Inner { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="NotExpression"/>.
    /// </summary>
    /// <param name="inner">The expression to negate.</param>
    public NotExpression(IQueryFilterExpression inner) =>
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
}
