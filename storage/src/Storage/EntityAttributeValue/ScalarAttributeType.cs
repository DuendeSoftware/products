// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a scalar (primitive) attribute type.
/// </summary>
public sealed record ScalarAttributeType : AttributeType
{
    public ScalarAttributeType(ScalarDataType DataType)
    {
        if (!Enum.IsDefined(DataType))
        {
            throw new ArgumentException($"Invalid ScalarDataType value: {DataType}.", nameof(DataType));
        }

        this.DataType = DataType;
    }

    public ScalarDataType DataType { get; init; }
}
