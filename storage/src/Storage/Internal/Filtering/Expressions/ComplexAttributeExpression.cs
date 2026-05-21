// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

public sealed class ComplexAttributeExpression(AttributePathExpression attributePath, FilterExpression filter)
    : FilterExpression
{
    public AttributePathExpression AttributePath { get; }
        = attributePath ?? throw new ArgumentNullException(nameof(attributePath));

    public FilterExpression Filter { get; }
        = filter ?? throw new ArgumentNullException(nameof(filter));

    public override string ToString() => $"{AttributePath}[{Filter}]";
}
