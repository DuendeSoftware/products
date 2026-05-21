// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a complex (nested object) attribute type with named properties.
/// </summary>
public sealed record ComplexAttributeType : AttributeType
{
    public ComplexAttributeType(IReadOnlyDictionary<AttributeCode, ComplexAttributeProperty> Properties)
    {
        ArgumentNullException.ThrowIfNull(Properties, nameof(Properties));

        if (Properties.Count == 0)
        {
            throw new ArgumentException("Properties must contain at least one entry.", nameof(Properties));
        }

        foreach (var (_, value) in Properties)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Properties));
        }

        // AttributeCode already implements case-insensitive Equals/GetHashCode via the generated code.
        var dict = new Dictionary<AttributeCode, ComplexAttributeProperty>();
        foreach (var (k, v) in Properties)
        {
            dict[k] = v;
        }
        this.Properties = dict;
    }

    /// <summary>
    ///     The named sub-properties and their types. All properties are optional — complex
    ///     values may contain any subset of the defined properties. Unknown properties
    ///     (not listed here) are rejected during validation.
    /// </summary>
    public IReadOnlyDictionary<AttributeCode, ComplexAttributeProperty> Properties { get; }

    /// <summary>
    ///     Tries to get a property by name (case-insensitive) and returns the schema-canonical
    ///     key alongside the property. This ensures callers can normalize to the schema-defined casing.
    /// </summary>
    public bool TryGetProperty(string name, [NotNullWhen(true)] out AttributeCode? canonicalKey, out ComplexAttributeProperty property)
    {
        if (!AttributeCode.TryCreate(name, out var code))
        {
            canonicalKey = null;
            property = default!;
            return false;
        }

        // The underlying dictionary uses AttributeCode.Comparer (OrdinalIgnoreCase), so TryGetValue matches case-insensitively.
        // To recover the canonical key we walk Keys — the dictionary is small (schema-defined properties).
        if (Properties.TryGetValue(code, out property!))
        {
            foreach (var key in Properties.Keys)
            {
                if (key == code)
                {
                    canonicalKey = key;
                    return true;
                }
            }
        }

        canonicalKey = null;
        property = default!;
        return false;
    }

    public bool Equals(ComplexAttributeType? other)
    {
        if (other is null)
        {
            return false;
        }

        if (Properties.Count != other.Properties.Count)
        {
            return false;
        }

        foreach (var (key, value) in Properties)
        {
            if (!other.Properties.TryGetValue(key, out var otherValue) || !value.Equals(otherValue))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (key, value) in Properties.OrderBy(p => p.Key.Value, StringComparer.OrdinalIgnoreCase))
        {
            hash.Add(key);
            hash.Add(value);
        }
        return hash.ToHashCode();
    }
}
