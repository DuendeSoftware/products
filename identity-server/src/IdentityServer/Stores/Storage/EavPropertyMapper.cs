// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// Generic helpers for serializing and deserializing extended attribute values
/// between <see cref="AttributeValueEntryDso"/> DSO lists and <see cref="AttributeValueCollection"/>.
/// Used by all configuration store admins and stores that support schema-validated extended properties.
/// </summary>
internal static class EavPropertyMapper
{
    /// <summary>
    /// Deserializes DSO entries into an <see cref="AttributeValueCollection"/>.
    /// Returns an empty collection when <paramref name="extendedEntries"/> is null or empty.
    /// </summary>
    internal static AttributeValueCollection DeserializeToCollection(
        IReadOnlyList<AttributeValueEntryDso>? extendedEntries)
    {
        if (extendedEntries is null || extendedEntries.Count == 0)
        {
            return new AttributeValueCollection();
        }

        var collection = new AttributeValueCollection();
        foreach (var entry in extendedEntries)
        {
            var code = AttributeCode.Create(entry.Code);
            switch (entry.DataType)
            {
                case "string":
                    collection.Set(code, entry.SerializedValue);
                    break;
                case "integer":
                    collection.Set(code, int.Parse(entry.SerializedValue, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "boolean":
                    collection.Set(code, bool.Parse(entry.SerializedValue));
                    break;
                case "decimal":
                    collection.Set(code, decimal.Parse(entry.SerializedValue, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "date":
                    collection.Set(code, DateOnly.ParseExact(entry.SerializedValue, "O", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "datetime":
                    collection.Set(code, DateTimeOffset.ParseExact(entry.SerializedValue, "O", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown data type '{entry.DataType}' for attribute '{entry.Code}'.");
            }
        }
        return collection;
    }

    /// <summary>
    /// Extracts string-typed attributes as a plain dictionary for populating
    /// runtime model <c>Properties</c> dictionaries (e.g., <see cref="Duende.IdentityServer.Models.Client.Properties"/>).
    /// Returns an empty dictionary when <paramref name="extendedEntries"/> is null or empty.
    /// </summary>
    internal static Dictionary<string, string> ExtractStringProperties(
        IReadOnlyList<AttributeValueEntryDso>? extendedEntries)
    {
        if (extendedEntries is null || extendedEntries.Count == 0)
        {
            return [];
        }

        var result = new Dictionary<string, string>();
        foreach (var e in extendedEntries)
        {
            if (e.DataType == "string")
            {
                result[e.Code] = e.SerializedValue;
            }
        }
        return result;
    }

    /// <summary>
    /// Serializes an <see cref="AttributeValueCollection"/> to the list of DSO entries
    /// used in a DSO's <c>ExtendedAttributeValues</c> property.
    /// </summary>
    internal static List<AttributeValueEntryDso> SerializeFromCollection(AttributeValueCollection props)
    {
        if (props.Count == 0)
        {
            return [];
        }

        return props.Select(attr => attr switch
        {
            AttributeValue<string> s => new AttributeValueEntryDso(attr.Code.Value, "string", s.TypedValue),
            AttributeValue<int> i => new AttributeValueEntryDso(attr.Code.Value, "integer", i.TypedValue.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            AttributeValue<bool> b => new AttributeValueEntryDso(attr.Code.Value, "boolean", b.TypedValue ? "true" : "false"),
            AttributeValue<decimal> d => new AttributeValueEntryDso(attr.Code.Value, "decimal", d.TypedValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture)),
            AttributeValue<DateOnly> dt => new AttributeValueEntryDso(attr.Code.Value, "date", dt.TypedValue.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
            AttributeValue<DateTimeOffset> dto => new AttributeValueEntryDso(attr.Code.Value, "datetime", dto.TypedValue.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
            _ => throw new InvalidOperationException($"Unsupported attribute value type for '{attr.Code}': {attr.GetType().Name}")
        }).ToList();
    }
}
