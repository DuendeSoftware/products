// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Helpers for serializing and deserializing extended attribute values
/// between <see cref="AttributeValueDso.V1"/> DSO lists and <see cref="AttributeValueCollection"/> or
/// <see cref="AttributeValue"/> sequences.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption,
/// and is not subject to semantic versioning rules.
/// </remarks>
internal static class EavMapper
{
    /// <summary>
    /// Serializes an <see cref="AttributeValueCollection"/> to a list of DSO entries.
    /// Scalar values are stored as their native CLR types; date and datetime values are stored
    /// as ISO-8601 strings to preserve precision across serialization round-trips.
    /// Complex and list values are stored as-is.
    /// </summary>
    internal static List<AttributeValueDso.V1> ToDsoList(AttributeValueCollection attributes)
    {
        if (attributes.Count == 0)
        {
            return [];
        }

        return [.. attributes.Select(ToDso)];
    }

    /// <summary>
    /// Serializes an <see cref="AttributeValue"/> sequence to a list of DSO entries.
    /// </summary>
    internal static List<AttributeValueDso.V1> ToDsoList(IEnumerable<AttributeValue> attributes) =>
        [.. attributes.Select(ToDso)];

    /// <summary>
    /// Deserializes DSO entries into an <see cref="AttributeValueCollection"/> using the provided schema
    /// to determine each attribute's type. Attributes not present in the schema are silently skipped.
    /// Complex and list attribute types are supported.
    /// </summary>
    internal static IEnumerable<AttributeValue> ToAttributeValues(
        IReadOnlyList<AttributeValueDso.V1> dsos,
        IReadOnlyAttributeSchema? schema)
    {
        if (schema is null)
        {
            yield break;
        }

        foreach (var dso in dsos)
        {
            var code = AttributeCode.Load(dso.Name);
            if (!schema.AttributeDefinitions.TryGetValue(code, out var definition))
            {
                continue;
            }

            if (definition.AttributeType is not ScalarAttributeType)
            {
                var normalized = dso.Value is JsonElement je ? NormalizeJsonElement(je) : dso.Value;
                if (normalized is IReadOnlyDictionary<string, object> dict)
                {
                    yield return AttributeValue.Load(code, dict);
                }
                else if (normalized is IReadOnlyList<object> list)
                {
                    yield return AttributeValue.Load(code, list);
                }
                continue;
            }

            var stringValue = dso.Value as string
                ?? (dso.Value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : dso.Value?.ToString());
            switch (definition.DataType)
            {
                case ScalarDataType.Boolean:
                    if (bool.TryParse(stringValue, out var boolValue))
                    {
                        yield return AttributeValue.Load(code, boolValue);
                    }
                    continue;
                case ScalarDataType.Date:
                    if (DateOnly.TryParse(stringValue, CultureInfo.InvariantCulture, out var dateValue))
                    {
                        yield return AttributeValue.Load(code, dateValue);
                    }
                    continue;
                case ScalarDataType.DateTime:
                    if (DateTimeOffset.TryParse(stringValue, CultureInfo.InvariantCulture, out var dateTimeValue))
                    {
                        yield return AttributeValue.Load(code, dateTimeValue);
                    }
                    continue;
                case ScalarDataType.Decimal:
                    if (decimal.TryParse(stringValue, CultureInfo.InvariantCulture, out var decimalValue))
                    {
                        yield return AttributeValue.Load(code, decimalValue);
                    }
                    continue;
                case ScalarDataType.Integer:
                    if (int.TryParse(stringValue, CultureInfo.InvariantCulture, out var intValue))
                    {
                        yield return AttributeValue.Load(code, intValue);
                    }
                    continue;
                case ScalarDataType.String:
                    if (stringValue is not null)
                    {
                        yield return AttributeValue.Load(code, stringValue);
                    }
                    continue;
                default:
                    throw new InvalidOperationException(
                        $"The {code} schema attribute has an unknown data type: {definition.DataType}");
            }
        }
    }

    /// <summary>
    /// Converts a read-only collection of <see cref="AttributeValue"/> instances to a mutable
    /// <see cref="AttributeValueCollection"/>.
    /// </summary>
    internal static AttributeValueCollection ToMutableCollection(IReadOnlyCollection<AttributeValue> attributes)
    {
        var collection = new AttributeValueCollection();
        foreach (var attr in attributes)
        {
            collection.Set(attr);
        }
        return collection;
    }

    private static AttributeValueDso.V1 ToDso(AttributeValue attribute) => new(
        attribute.Code.Value,
        attribute.UntypedValue switch
        {
            null => null,
            string s => s,
            bool b => b,
            int i => i,
            decimal d => d,
            IReadOnlyDictionary<string, object> or IReadOnlyList<object> => attribute.UntypedValue,
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => attribute.UntypedValue.ToString()!
        });

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a CLR object suitable for domain use.
    /// </summary>
    internal static object? NormalizeJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetDecimal(out var d) ? d : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.Object => (object)element.EnumerateObject()
                .ToDictionary(p => p.Name, p => NormalizeJsonElement(p.Value))
                .AsReadOnly(),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(NormalizeJsonElement)
                .ToList()
                .AsReadOnly(),
            _ => element.GetRawText()
        };
}
