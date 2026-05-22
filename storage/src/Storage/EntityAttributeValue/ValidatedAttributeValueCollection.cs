// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     An immutable, validated collection of attribute values that has passed schema validation.
/// </summary>
public sealed class ValidatedAttributeValueCollection : IEnumerable<AttributeValue>
{
    private readonly FrozenDictionary<AttributeCode, AttributeValue> _dict;

    /// <summary>
    ///     Gets an empty validated collection with no attribute values.
    /// </summary>
    public static ValidatedAttributeValueCollection Empty { get; } =
        new([], UuidV7.Load(Guid.Empty), 0);

    internal ValidatedAttributeValueCollection(IEnumerable<AttributeValue> attributes, UuidV7 schemaId, int schemaVersion)
    {
        _dict = attributes.ToFrozenDictionary(a => a.Code);
        SchemaId = schemaId;
        SchemaVersion = schemaVersion;
    }

    internal UuidV7 SchemaId { get; }
    internal int SchemaVersion { get; }

    /// <summary>
    ///     Gets the number of attribute values in the collection.
    /// </summary>
    public int Count => _dict.Count;

    /// <summary>
    ///     Determines whether an attribute with the specified code exists in the collection.
    /// </summary>
    /// <param name="code">The attribute code to check.</param>
    /// <returns><c>true</c> if the attribute exists; otherwise, <c>false</c>.</returns>
    public bool Contains(AttributeCode code) => _dict.ContainsKey(code);

    /// <summary>
    ///     Attempts to retrieve an attribute value by code.
    /// </summary>
    /// <param name="code">The attribute code to look up.</param>
    /// <param name="attribute">The attribute value if found.</param>
    /// <returns><c>true</c> if the attribute was found; otherwise, <c>false</c>.</returns>
    public bool TryGet(AttributeCode code, [MaybeNullWhen(false)] out AttributeValue attribute) =>
        _dict.TryGetValue(code, out attribute);

    /// <summary>
    ///     Gets the attribute value with the specified code.
    /// </summary>
    /// <param name="code">The attribute code.</param>
    /// <returns>The attribute value.</returns>
#pragma warning disable CA1043 // Use integral or string argument for indexers
    public AttributeValue this[AttributeCode code] => _dict[code];
#pragma warning restore CA1043

    /// <summary>
    ///     Returns an enumerator that iterates through the attribute values.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    public IEnumerator<AttributeValue> GetEnumerator() => ((IEnumerable<AttributeValue>)_dict.Values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
