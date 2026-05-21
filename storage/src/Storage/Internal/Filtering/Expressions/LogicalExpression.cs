// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

public sealed class LogicalExpression : FilterExpression
{
    public LogicalOperator Operator { get; }

    public FilterExpression Left { get; }

    public FilterExpression? Right { get; }

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

    public override string ToString() =>
        Operator switch
        {
            LogicalOperator.And => $"({Left} AND {Right})",
            LogicalOperator.Or => $"({Left} OR {Right})",
            LogicalOperator.Not => $"NOT ({Left})",
            _ => $"Unknown operator: {Operator}"
        };
}
