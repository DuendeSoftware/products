// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

/// <summary>
/// Represents a complex attribute filter expression with a nested filter (e.g., emails[type eq "work"]).
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="attributePath">The attribute path containing sub-attributes.</param>
/// <param name="filter">The nested filter expression applied to the complex attribute.</param>
public sealed class ComplexAttributeExpression(AttributePathExpression attributePath, FilterExpression filter)
    : FilterExpression
{
    /// <summary>Gets the attribute path.</summary>
    public AttributePathExpression AttributePath { get; }
        = attributePath ?? throw new ArgumentNullException(nameof(attributePath));

    /// <summary>Gets the nested filter expression.</summary>
    public FilterExpression Filter { get; }
        = filter ?? throw new ArgumentNullException(nameof(filter));

    /// <inheritdoc />
    public override string ToString() => $"{AttributePath}[{Filter}]";
}
