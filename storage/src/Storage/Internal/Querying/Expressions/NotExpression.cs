// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that negates an inner filter expression.
/// Used for SCIM 'not' operator and 'ne' (as Not(Equal(...))).
/// </summary>
public sealed record NotExpression : IQueryFilterExpression
{
    /// <summary>
    /// The inner expression to negate.
    /// </summary>
    public IQueryFilterExpression Inner { get; }

    public NotExpression(IQueryFilterExpression inner) =>
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
}
