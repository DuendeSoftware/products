// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Extension methods for fluent composition of query filter expressions.
/// </summary>
public static class QueryFilterExpressionExtensions
{
    /// <summary>
    /// Combines this expression with another using AND logic.
    /// Smart accumulation: if called on an AndExpression, adds to Parts collection instead of nesting.
    /// </summary>
    public static IQueryFilterExpression And(this IQueryFilterExpression left, IQueryFilterExpression right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        // Smart accumulation for AndExpression
        if (left is AndExpression andExpression)
        {
            return andExpression.And(right);
        }

        // Smart accumulation: flatten if right is also AndExpression
        if (right is AndExpression rightAnd)
        {
            var parts = new List<IQueryFilterExpression> { left };
            parts.AddRange(rightAnd.Parts);
            return new AndExpression(parts);
        }

        return new AndExpression(left, right);
    }

    /// <summary>
    /// Combines this expression with another using OR logic.
    /// Smart accumulation: if called on an OrExpression, adds to Parts collection instead of nesting.
    /// </summary>
    public static IQueryFilterExpression Or(this IQueryFilterExpression left, IQueryFilterExpression right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        // Smart accumulation for OrExpression
        if (left is OrExpression orExpression)
        {
            return orExpression.Or(right);
        }

        // Smart accumulation: flatten if right is also OrExpression
        if (right is OrExpression rightOr)
        {
            var parts = new List<IQueryFilterExpression> { left };
            parts.AddRange(rightOr.Parts);
            return new OrExpression(parts);
        }

        return new OrExpression(left, right);
    }

}
