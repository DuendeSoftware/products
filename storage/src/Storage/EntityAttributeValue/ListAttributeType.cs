// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a list attribute type with a single element type.
///     Lists cannot be nested inside other lists.
/// </summary>
public sealed record ListAttributeType : AttributeType
{
    /// <summary>
    ///     Creates a list attribute type with the specified element type.
    /// </summary>
    /// <param name="ElementType">The type of elements in the list.</param>
    public ListAttributeType(AttributeType ElementType)
    {
        ArgumentNullException.ThrowIfNull(ElementType);

        this.ElementType = ElementType;

        // Validate no list-in-list at construction time
        ValidateNesting();
    }

    /// <summary>
    ///     Gets the type of elements in this list.
    /// </summary>
    public AttributeType ElementType { get; }

    /// <summary>
    ///     Determines whether this list type is equal to another.
    /// </summary>
    /// <param name="other">The other list attribute type.</param>
    /// <returns><c>true</c> if the types are equal; otherwise, <c>false</c>.</returns>
    public bool Equals(ListAttributeType? other) =>
        other is not null && ElementType.Equals(other.ElementType);

    /// <summary>
    ///     Returns the hash code for this list attribute type.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(ElementType);
}
