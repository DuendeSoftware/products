// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

public sealed class ValidatedAttributeValueCollection : IEnumerable<AttributeValue>
{
    private readonly FrozenDictionary<AttributeCode, AttributeValue> _dict;

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

    public int Count => _dict.Count;

    public bool Contains(AttributeCode code) => _dict.ContainsKey(code);

    public bool TryGet(AttributeCode code, [MaybeNullWhen(false)] out AttributeValue attribute) =>
        _dict.TryGetValue(code, out attribute);

#pragma warning disable CA1043 // Use integral or string argument for indexers
    public AttributeValue this[AttributeCode code] => _dict[code];
#pragma warning restore CA1043

    public IEnumerator<AttributeValue> GetEnumerator() => ((IEnumerable<AttributeValue>)_dict.Values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
