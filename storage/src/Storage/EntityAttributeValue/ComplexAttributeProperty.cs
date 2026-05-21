// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a sub-property within a <see cref="ComplexAttributeType" />,
///     bundling the property's type with optional display metadata.
/// </summary>
public sealed record ComplexAttributeProperty
{
    private ComplexAttributeProperty(AttributeType type, AttributeDisplayName? displayName, AttributeDescription? description)
    {
        Type = type;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>The type of this sub-property.</summary>
    public AttributeType Type { get; }

    /// <summary>Optional human-readable display name for this sub-property.</summary>
    public AttributeDisplayName? DisplayName { get; }

    /// <summary>Optional description for this sub-property.</summary>
    public AttributeDescription? Description { get; }

    /// <summary>Creates a property with a scalar type and no metadata.</summary>
    public static ComplexAttributeProperty Of(ScalarDataType dataType) =>
        new(new ScalarAttributeType(dataType), null, null);

    /// <summary>Creates a property with the given type and no metadata.</summary>
    public static ComplexAttributeProperty Of(AttributeType type) =>
        new(type, null, null);

    /// <summary>Creates a property with the given type and display name.</summary>
    public static ComplexAttributeProperty Of(AttributeType type, AttributeDisplayName? displayName) =>
        new(type, displayName, null);

    /// <summary>Creates a property with the given type and optional metadata.</summary>
    public static ComplexAttributeProperty Of(AttributeType type, AttributeDisplayName? displayName, AttributeDescription? description) =>
        new(type, displayName, description);
}
