// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.Internal;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Represents a version 1 data storage key for an attribute value, keyed by attribute code and value.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal sealed record AttributeValueDskV1 : IDataStorageKey
{
    private AttributeValueDskV1(string name, string value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>Gets the data storage key version descriptor.</summary>
    public static DataStorageKeyVersion DskVersion { get; } =
        new(new DataStorageKeyType(1u, "Attribute"), 1);

    /// <summary>Gets the attribute name (code).</summary>
    public string Name { get; }

    /// <summary>Gets the attribute value as an invariant string.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a key from an <see cref="AttributeValue"/>.
    /// </summary>
    /// <param name="attribute">The attribute value.</param>
    /// <returns>A new data storage key.</returns>
    public static AttributeValueDskV1 Create(AttributeValue attribute) =>
        new(attribute.Code.Value, ToInvariantString(attribute.UntypedValue));

    /// <summary>
    /// Creates a key from an attribute code and value.
    /// </summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>A new data storage key.</returns>
    public static AttributeValueDskV1 Create(AttributeCode code, object value) =>
        new(code.Value, ToInvariantString(value));

    private static string ToInvariantString(object value) =>
        value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()!
        };
}
