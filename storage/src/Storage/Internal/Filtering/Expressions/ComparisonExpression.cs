// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

public sealed class ComparisonExpression(AttributePathExpression attributePath, ComparisonOperator op, object? value)
    : FilterExpression
{
    public AttributePathExpression AttributePath { get; } = attributePath ?? throw new ArgumentNullException(nameof(attributePath));

    public ComparisonOperator Operator { get; } = op;

    public object? Value { get; } = value;

    public override string ToString() => $"{AttributePath} {Operator.ToFilterString()} {Value}";
}
