// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.Internal;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

public sealed record AttributeValueDskV1 : IDataStorageKey
{
    private AttributeValueDskV1(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public static DataStorageKeyVersion DskVersion { get; } =
        new(new DataStorageKeyType(1u, "Attribute"), 1);

    public string Name { get; }

    public string Value { get; }

    public static AttributeValueDskV1 Create(AttributeValue attribute) =>
        new(attribute.Code.Value, ToInvariantString(attribute.UntypedValue));

    public static AttributeValueDskV1 Create(AttributeCode code, object value) =>
        new(code.Value, ToInvariantString(value));

    private static string ToInvariantString(object value) =>
        value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()!
        };
}
