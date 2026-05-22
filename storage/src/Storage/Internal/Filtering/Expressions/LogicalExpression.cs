// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

/// <summary>
/// Represents a logical filter expression combining sub-expressions with AND, OR, or NOT.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class LogicalExpression : FilterExpression
{
    /// <summary>Gets the logical operator.</summary>
    public LogicalOperator Operator { get; }

    /// <summary>Gets the left (or only, for NOT) operand.</summary>
    public FilterExpression Left { get; }

    /// <summary>Gets the right operand, or null for NOT expressions.</summary>
    public FilterExpression? Right { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="LogicalExpression"/>.
    /// </summary>
    /// <param name="op">The logical operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand (required for AND/OR, must be null for NOT).</param>
    public LogicalExpression(LogicalOperator op, FilterExpression left, FilterExpression? right = null)
    {
        Operator = op;
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right;

        if (op != LogicalOperator.Not && right == null)
        {
            throw new ArgumentException($"{op} operator requires a right operand", nameof(right));
        }
        if (op == LogicalOperator.Not && right != null)
        {
            throw new ArgumentException("NOT operator should not have a right operand", nameof(right));
        }
    }

    /// <inheritdoc />
    public override string ToString() =>
        Operator switch
        {
            LogicalOperator.And => $"({Left} AND {Right})",
            LogicalOperator.Or => $"({Left} OR {Right})",
            LogicalOperator.Not => $"NOT ({Left})",
            _ => $"Unknown operator: {Operator}"
        };
}
