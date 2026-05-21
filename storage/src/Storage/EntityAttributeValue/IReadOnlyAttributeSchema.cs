// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

public interface IReadOnlyAttributeSchema
{
    IReadOnlyDictionary<AttributeCode, AttributeDefinition> AttributeDefinitions { get; }

    /// <summary>
    ///     The named groups defined in this schema, keyed by group name.
    /// </summary>
    IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups { get; }

    AttributeValue<bool> CreateAttribute(AttributeCode code, bool value);

    AttributeValue<DateOnly> CreateAttribute(AttributeCode code, DateOnly value);

    AttributeValue<DateTimeOffset> CreateAttribute(AttributeCode code, DateTimeOffset value);

    AttributeValue<decimal> CreateAttribute(AttributeCode code, decimal value);

    AttributeValue<int> CreateAttribute(AttributeCode code, int value);

    AttributeValue<string> CreateAttribute(AttributeCode code, string value);

    AttributeValue<IReadOnlyDictionary<string, object>> CreateAttribute(AttributeCode code, IReadOnlyDictionary<string, object> complexValue);

    AttributeValue<IReadOnlyList<object>> CreateAttribute(AttributeCode code, IReadOnlyList<object> listValue);

    bool TryCreateAttribute(AttributeCode code, bool value, [NotNullWhen(true)] out AttributeValue<bool>? attribute);

    bool TryCreateAttribute(AttributeCode code, DateOnly value, [NotNullWhen(true)] out AttributeValue<DateOnly>? attribute);

    bool TryCreateAttribute(AttributeCode code, DateTimeOffset value, [NotNullWhen(true)] out AttributeValue<DateTimeOffset>? attribute);

    bool TryCreateAttribute(AttributeCode code, decimal value, [NotNullWhen(true)] out AttributeValue<decimal>? attribute);

    bool TryCreateAttribute(AttributeCode code, int value, [NotNullWhen(true)] out AttributeValue<int>? attribute);

    bool TryCreateAttribute(AttributeCode code, string value, [NotNullWhen(true)] out AttributeValue<string>? attribute);

    bool TryCreateAttribute(AttributeCode code, IReadOnlyDictionary<string, object> complexValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyDictionary<string, object>>? attribute);

    bool TryCreateAttribute(AttributeCode code, IReadOnlyList<object> listValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyList<object>>? attribute);

    bool TryCreateAttribute(AttributeCode code, bool value, [NotNullWhen(true)] out AttributeValue<bool>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    bool TryCreateAttribute(AttributeCode code, DateOnly value, [NotNullWhen(true)] out AttributeValue<DateOnly>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    bool TryCreateAttribute(AttributeCode code, DateTimeOffset value, [NotNullWhen(true)] out AttributeValue<DateTimeOffset>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    bool TryCreateAttribute(AttributeCode code, decimal value, [NotNullWhen(true)] out AttributeValue<decimal>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    bool TryCreateAttribute(AttributeCode code, int value, [NotNullWhen(true)] out AttributeValue<int>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    bool TryCreateAttribute(AttributeCode code, string value, [NotNullWhen(true)] out AttributeValue<string>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    bool TryCreateAttribute(AttributeCode code, IReadOnlyDictionary<string, object> complexValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyDictionary<string, object>>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    bool TryCreateAttribute(AttributeCode code, IReadOnlyList<object> listValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyList<object>>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors);

    AttributeValueCollection CreateAttributes(IEnumerable<AttributeValue> attributes);
}
