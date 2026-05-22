// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that combines multiple filter expressions with AND logic.
/// All conditions must be true for the expression to match.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record AndExpression : IQueryFilterExpression
{
    /// <summary>
    /// The collection of expressions that must all be true.
    /// </summary>
    public IReadOnlyList<IQueryFilterExpression> Parts { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AndExpression"/> with the specified parts.
    /// </summary>
    /// <param name="parts">The filter expressions to combine with AND logic.</param>
    public AndExpression(IReadOnlyList<IQueryFilterExpression> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count == 0)
        {
            throw new ArgumentException("AndExpression must have at least one part.", nameof(parts));
        }

        Parts = parts;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AndExpression"/> with the specified parts.
    /// </summary>
    /// <param name="parts">The filter expressions to combine with AND logic.</param>
    public AndExpression(params IQueryFilterExpression[] parts)
        : this((IReadOnlyList<IQueryFilterExpression>)parts)
    {
    }

    /// <summary>
    /// Adds another condition with AND logic.
    /// Smart accumulation: if called on an AndExpression, adds to Parts collection instead of nesting.
    /// </summary>
    public IQueryFilterExpression And(IQueryFilterExpression other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Smart accumulation: flatten nested AndExpressions
        var newParts = new List<IQueryFilterExpression>(Parts);

        if (other is AndExpression andExpression)
        {
            // Flatten: add all parts from the nested AndExpression
            newParts.AddRange(andExpression.Parts);
        }
        else
        {
            newParts.Add(other);
        }

        return new AndExpression(newParts);
    }

    /// <summary>
    /// Combines this expression with another using OR logic.
    /// </summary>
    public IQueryFilterExpression Or(IQueryFilterExpression other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new OrExpression(this, other);
    }
}
