// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering;

internal static class OperatorExtensions
{
    public static string ToFilterString(this ComparisonOperator op) =>
        op switch
        {
            ComparisonOperator.Equal => "eq",
            ComparisonOperator.NotEqual => "ne",
            ComparisonOperator.Contains => "co",
            ComparisonOperator.StartsWith => "sw",
            ComparisonOperator.EndsWith => "ew",
            ComparisonOperator.GreaterThan => "gt",
            ComparisonOperator.GreaterThanOrEqual => "ge",
            ComparisonOperator.LessThan => "lt",
            ComparisonOperator.LessThanOrEqual => "le",
            ComparisonOperator.Present => "pr",
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    public static string ToFilterString(this LogicalOperator op) =>
        op switch
        {
            LogicalOperator.And => "and",
            LogicalOperator.Or => "or",
            LogicalOperator.Not => "not",
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
}
