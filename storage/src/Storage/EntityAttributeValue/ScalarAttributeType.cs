// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a scalar (primitive) attribute type.
/// </summary>
public sealed record ScalarAttributeType : AttributeType
{
    /// <summary>
    ///     Creates a scalar attribute type with the specified data type.
    /// </summary>
    /// <param name="DataType">The scalar data type.</param>
    public ScalarAttributeType(ScalarDataType DataType)
    {
        if (!Enum.IsDefined(DataType))
        {
            throw new ArgumentException($"Invalid ScalarDataType value: {DataType}.", nameof(DataType));
        }

        this.DataType = DataType;
    }

    /// <summary>
    ///     Gets the scalar data type for this attribute.
    /// </summary>
    public ScalarDataType DataType { get; init; }
}
