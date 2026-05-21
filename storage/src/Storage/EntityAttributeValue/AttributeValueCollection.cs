// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

public sealed class AttributeValueCollection : IEnumerable<AttributeValue>
{
    private readonly Dictionary<AttributeCode, AttributeValue> _dict = [];

    public AttributeValueCollection() { }

    public AttributeValueCollection(IEnumerable<AttributeValue> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (!_dict.TryAdd(attribute.Code, attribute))
            {
                throw new ArgumentException(
                    $"The attributes contain more than one attribute named '{attribute.Code}'", nameof(attributes));
            }
        }
    }

    public void Set(AttributeValue attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        _dict[attribute.Code] = attribute;
    }

    public int Count => _dict.Count;

    public bool Remove(AttributeCode code) => _dict.Remove(code);

    public bool Contains(AttributeCode code) => _dict.ContainsKey(code);

    public bool TryGet(AttributeCode code, [MaybeNullWhen(false)] out AttributeValue attribute) =>
        _dict.TryGetValue(code, out attribute);

#pragma warning disable CA1043 // Use integral or string argument for indexers
    public AttributeValue this[AttributeCode code] => _dict[code];
#pragma warning restore CA1043

    public IEnumerator<AttributeValue> GetEnumerator() => _dict.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
