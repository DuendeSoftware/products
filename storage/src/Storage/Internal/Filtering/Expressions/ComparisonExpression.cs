// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

/// <summary>
/// Represents a comparison filter expression (e.g., attribute eq value).
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="attributePath">The attribute path being compared.</param>
/// <param name="op">The comparison operator.</param>
/// <param name="value">The value to compare against.</param>
public sealed class ComparisonExpression(AttributePathExpression attributePath, ComparisonOperator op, object? value)
    : FilterExpression
{
    /// <summary>Gets the attribute path being compared.</summary>
    public AttributePathExpression AttributePath { get; } = attributePath ?? throw new ArgumentNullException(nameof(attributePath));

    /// <summary>Gets the comparison operator.</summary>
    public ComparisonOperator Operator { get; } = op;

    /// <summary>Gets the comparison value.</summary>
    public object? Value { get; } = value;

    /// <inheritdoc />
    public override string ToString() => $"{AttributePath} {Operator.ToFilterString()} {Value}";
}
