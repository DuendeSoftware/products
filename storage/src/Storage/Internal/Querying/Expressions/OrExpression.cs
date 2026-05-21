// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that combines multiple filter expressions with OR logic.
/// At least one condition must be true for the expression to match.
/// </summary>
public sealed record OrExpression : IQueryFilterExpression
{
    /// <summary>
    /// The collection of expressions where at least one must be true.
    /// </summary>
    public IReadOnlyList<IQueryFilterExpression> Parts { get; }

    public OrExpression(IReadOnlyList<IQueryFilterExpression> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count == 0)
        {
            throw new ArgumentException("OrExpression must have at least one part.", nameof(parts));
        }

        Parts = parts;
    }

    public OrExpression(params IQueryFilterExpression[] parts)
        : this((IReadOnlyList<IQueryFilterExpression>)parts)
    {
    }

    /// <summary>
    /// Combines this expression with another using AND logic.
    /// </summary>
    public IQueryFilterExpression And(IQueryFilterExpression other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new AndExpression(this, other);
    }

    /// <summary>
    /// Adds another condition with OR logic.
    /// Smart accumulation: if called on an OrExpression, adds to Parts collection instead of nesting.
    /// </summary>
    public IQueryFilterExpression Or(IQueryFilterExpression other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Smart accumulation: flatten nested OrExpressions
        var newParts = new List<IQueryFilterExpression>(Parts);

        if (other is OrExpression orExpression)
        {
            // Flatten: add all parts from the nested OrExpression
            newParts.AddRange(orExpression.Parts);
        }
        else
        {
            newParts.Add(other);
        }

        return new OrExpression(newParts);
    }
}
