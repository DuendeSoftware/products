// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a list attribute type with a single element type.
///     Lists cannot be nested inside other lists.
/// </summary>
public sealed record ListAttributeType : AttributeType
{
    public ListAttributeType(AttributeType ElementType)
    {
        ArgumentNullException.ThrowIfNull(ElementType);

        this.ElementType = ElementType;

        // Validate no list-in-list at construction time
        ValidateNesting();
    }

    public AttributeType ElementType { get; }

    public bool Equals(ListAttributeType? other) =>
        other is not null && ElementType.Equals(other.ElementType);

    public override int GetHashCode() => HashCode.Combine(ElementType);
}
