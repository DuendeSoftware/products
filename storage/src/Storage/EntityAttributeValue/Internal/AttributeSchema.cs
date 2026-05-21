// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Storage.Internal.Querying;

namespace Duende.Storage.EntityAttributeValue.Internal;

/// <summary>
///     Represents a dynamic collection of attributes.
/// </summary>
public sealed class AttributeSchema : IReadOnlyAttributeSchema
{
    private readonly Dictionary<AttributeCode, AttributeDefinition> _attributesDefinitions;
    private readonly Dictionary<AttributeGroupCode, AttributeGroup> _groups;

    private AttributeSchema(IEnumerable<AttributeDefinition> attributeDefinitions, IEnumerable<AttributeGroup> groups)
    {
        _attributesDefinitions = new Dictionary<AttributeCode, AttributeDefinition>();
        foreach (var d in attributeDefinitions)
        {
            _attributesDefinitions[d.Code] = d; // Last-write-wins for duplicates from storage
        }

        _groups = new Dictionary<AttributeGroupCode, AttributeGroup>();
        foreach (var g in groups)
        {
            _groups[g.Code] = g; // Last-write-wins for duplicates from storage
        }
    }

    public AttributeSchema() : this([], [])
    {
    }

    public IReadOnlyDictionary<AttributeCode, AttributeDefinition> AttributeDefinitions => _attributesDefinitions;

    public IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups => _groups;

    public bool AddGroup(AttributeGroup group) => _groups.TryAdd(group.Code, group);

    public bool RemoveGroup(AttributeGroupCode name)
    {
        if (!_groups.Remove(name))
        {
            return false;
        }

        // Ungroup all attributes that referenced this group
        var toUngroup = _attributesDefinitions.Values
            .Where(d => d.GroupCode != null && d.GroupCode.Equals(name))
            .ToList();

        foreach (var definition in toUngroup)
        {
            _attributesDefinitions[definition.Code] = AttributeDefinition.Load(
                definition.Code,
                definition.AttributeType,
                definition.Description,
                definition.IsUnique,
                definition.Tags,
                null,
                definition.Order);
        }

        return true;
    }

    public bool UpdateGroup(AttributeGroup group)
    {
        if (!_groups.ContainsKey(group.Code))
        {
            return false;
        }

        _groups[group.Code] = group;
        return true;
    }

    public bool AddAttributeDefinition(AttributeDefinition definition)
    {
        if (SystemFields.IsReservedAttributeName(definition.Code.Value))
        {
            return false;
        }

        if (definition.GroupCode != null && !_groups.ContainsKey(definition.GroupCode))
        {
            return false;
        }

        return _attributesDefinitions.TryAdd(definition.Code, definition);
    }

    public void RemoveAttributeDefinition(AttributeCode code) => _ = _attributesDefinitions.Remove(code);

    public AttributeValue<bool> CreateAttribute(AttributeCode code, bool value) =>
        TryCreateAttribute(code, value, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public AttributeValue<DateOnly> CreateAttribute(AttributeCode code, DateOnly value) =>
        TryCreateAttribute(code, value, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public AttributeValue<DateTimeOffset> CreateAttribute(AttributeCode code, DateTimeOffset value) =>
        TryCreateAttribute(code, value, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public AttributeValue<decimal> CreateAttribute(AttributeCode code, decimal value) =>
        TryCreateAttribute(code, value, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public AttributeValue<int> CreateAttribute(AttributeCode code, int value) =>
        TryCreateAttribute(code, value, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public AttributeValue<string> CreateAttribute(AttributeCode code, string value) =>
        TryCreateAttribute(code, value, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public AttributeValue<IReadOnlyDictionary<string, object>> CreateAttribute(AttributeCode code, IReadOnlyDictionary<string, object> complexValue) =>
        TryCreateAttribute(code, complexValue, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public AttributeValue<IReadOnlyList<object>> CreateAttribute(AttributeCode code, IReadOnlyList<object> listValue) =>
        TryCreateAttribute(code, listValue, out var attribute, out var errors)
            ? attribute
            : throw new ArgumentException(string.Join("; ", errors));

    public bool TryCreateAttribute(AttributeCode code, bool value, [NotNullWhen(true)] out AttributeValue<bool>? attribute) =>
        TryCreateAttribute(code, value, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, DateOnly value, [NotNullWhen(true)] out AttributeValue<DateOnly>? attribute) =>
        TryCreateAttribute(code, value, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, DateTimeOffset value, [NotNullWhen(true)] out AttributeValue<DateTimeOffset>? attribute) =>
        TryCreateAttribute(code, value, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, decimal value, [NotNullWhen(true)] out AttributeValue<decimal>? attribute) =>
        TryCreateAttribute(code, value, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, int value, [NotNullWhen(true)] out AttributeValue<int>? attribute) =>
        TryCreateAttribute(code, value, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, string value, [NotNullWhen(true)] out AttributeValue<string>? attribute) =>
        TryCreateAttribute(code, value, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, IReadOnlyDictionary<string, object> complexValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyDictionary<string, object>>? attribute) =>
        TryCreateAttribute(code, complexValue, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, IReadOnlyList<object> listValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyList<object>>? attribute) =>
        TryCreateAttribute(code, listValue, out attribute, out _);

    public bool TryCreateAttribute(AttributeCode code, bool value, [NotNullWhen(true)] out AttributeValue<bool>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TryCreateScalarAttribute(code, value, ScalarDataType.Boolean, out attribute, out errors);

    public bool TryCreateAttribute(AttributeCode code, DateOnly value, [NotNullWhen(true)] out AttributeValue<DateOnly>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TryCreateScalarAttribute(code, value, ScalarDataType.Date, out attribute, out errors);

    public bool TryCreateAttribute(AttributeCode code, DateTimeOffset value, [NotNullWhen(true)] out AttributeValue<DateTimeOffset>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TryCreateScalarAttribute(code, value, ScalarDataType.DateTime, out attribute, out errors);

    public bool TryCreateAttribute(AttributeCode code, decimal value, [NotNullWhen(true)] out AttributeValue<decimal>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TryCreateScalarAttribute(code, value, ScalarDataType.Decimal, out attribute, out errors);

    public bool TryCreateAttribute(AttributeCode code, int value, [NotNullWhen(true)] out AttributeValue<int>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TryCreateScalarAttribute(code, value, ScalarDataType.Integer, out attribute, out errors);

    public bool TryCreateAttribute(AttributeCode code, string value, [NotNullWhen(true)] out AttributeValue<string>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors)
    {
        if (!_attributesDefinitions.TryGetValue(code, out var definition))
        {
            attribute = null;
            errors = [$"Attribute '{code}' is not defined in the schema."];
            return false;
        }

        if (definition.AttributeType is not ScalarAttributeType scalar || scalar.DataType != ScalarDataType.String)
        {
            var providedType = typeof(string).Name;
            var definedType = definition.AttributeType is ScalarAttributeType s ? s.DataType.ToString() : definition.AttributeType.GetType().Name;
            attribute = null;
            errors = [$"Attribute '{code}' is defined as '{definedType}' but a '{providedType}' value was provided."];
            return false;
        }

        attribute = new AttributeValue<string>(code, value);
        errors = null;
        return true;
    }

    public bool TryCreateAttribute(AttributeCode code, IReadOnlyDictionary<string, object> complexValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyDictionary<string, object>>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors)
    {
        if (!_attributesDefinitions.TryGetValue(code, out var definition))
        {
            attribute = null;
            errors = [$"Attribute '{code}' is not defined in the schema."];
            return false;
        }

        if (definition.AttributeType is not ComplexAttributeType complexType)
        {
            attribute = null;
            errors = [$"Attribute '{code}' is not a complex type."];
            return false;
        }

        var errorList = new List<string>();
        CollectComplexValueErrors(code, complexValue, complexType, errorList);

        if (errorList.Count > 0)
        {
            attribute = null;
            errors = errorList;
            return false;
        }

        attribute = new AttributeValue<IReadOnlyDictionary<string, object>>(code, complexValue);
        errors = null;
        return true;
    }

    public bool TryCreateAttribute(AttributeCode code, IReadOnlyList<object> listValue, [NotNullWhen(true)] out AttributeValue<IReadOnlyList<object>>? attribute, [NotNullWhen(false)] out IReadOnlyList<string>? errors)
    {
        if (!_attributesDefinitions.TryGetValue(code, out var definition))
        {
            attribute = null;
            errors = [$"Attribute '{code}' is not defined in the schema."];
            return false;
        }

        if (definition.AttributeType is not ListAttributeType listType)
        {
            attribute = null;
            errors = [$"Attribute '{code}' is not a list type."];
            return false;
        }

        var errorList = new List<string>();
        CollectListValueErrors(code, listValue, listType, errorList);

        if (errorList.Count > 0)
        {
            attribute = null;
            errors = errorList;
            return false;
        }

        attribute = new AttributeValue<IReadOnlyList<object>>(code, listValue);
        errors = null;
        return true;
    }

    public AttributeValueCollection CreateAttributes(IEnumerable<AttributeValue> attributes) => new AttributeValueCollection(attributes);

    private bool TryCreateScalarAttribute<T>(AttributeCode code, T value, ScalarDataType dataType, out AttributeValue<T>? attribute, out IReadOnlyList<string>? errors)
    {
        if (!_attributesDefinitions.TryGetValue(code, out var definition))
        {
            attribute = null;
            errors = [$"Attribute '{code}' is not defined in the schema."];
            return false;
        }

        if (definition.AttributeType is not ScalarAttributeType scalar || scalar.DataType != dataType)
        {
            var providedType = typeof(T).Name;
            var definedType = definition.AttributeType is ScalarAttributeType s ? s.DataType.ToString() : definition.AttributeType.GetType().Name;
            attribute = null;
            errors = [$"Attribute '{code}' is defined as '{definedType}' but a '{providedType}' value was provided."];
            return false;
        }

        attribute = new AttributeValue<T>(code, value);
        errors = null;
        return true;
    }

    private static void CollectComplexValueErrors(AttributeCode code, IReadOnlyDictionary<string, object> value, ComplexAttributeType complexType, List<string> errors)
    {
        foreach (var (key, propValue) in value)
        {
            if (!complexType.TryGetProperty(key, out _, out var prop))
            {
                errors.Add($"Property '{key}' is not defined in complex attribute '{code}'.");
                continue;
            }

            var expectedType = GetExpectedTypeName(prop.Type);

            if (propValue is null)
            {
                errors.Add($"Property '{key}' in attribute '{code}' expects type '{expectedType}' but got 'null'.");
                continue;
            }

            if (!ValidateValueAgainstType(propValue, prop.Type))
            {
                var actualType = propValue.GetType().Name;
                errors.Add($"Property '{key}' in attribute '{code}' expects type '{expectedType}' but got '{actualType}'.");
            }
        }
    }

    private static void CollectListValueErrors(AttributeCode code, IReadOnlyList<object> value, ListAttributeType listType, List<string> errors)
    {
        for (var i = 0; i < value.Count; i++)
        {
            var element = value[i];

            if (listType.ElementType is ComplexAttributeType complexElementType)
            {
                if (element is null)
                {
                    errors.Add($"Element at index {i} in list attribute '{code}' expects type 'Complex' but got 'null'.");
                }
                else if (element is IReadOnlyDictionary<string, object> dict)
                {
                    var before = errors.Count;
                    CollectComplexValueErrors(code, dict, complexElementType, errors);
                    // Prefix element-level context to any errors added
                    for (var j = before; j < errors.Count; j++)
                    {
                        errors[j] = $"Element at index {i}: {errors[j]}";
                    }
                }
                else
                {
                    var actualType = element.GetType().Name;
                    errors.Add($"Element at index {i} in list attribute '{code}' expects type 'Complex' but got '{actualType}'.");
                }
            }
            else if (element is null)
            {
                var expectedType = GetExpectedTypeName(listType.ElementType);
                errors.Add($"Element at index {i} in list attribute '{code}' expects type '{expectedType}' but got 'null'.");
            }
            else if (!ValidateValueAgainstType(element, listType.ElementType))
            {
                var expectedType = GetExpectedTypeName(listType.ElementType);
                var actualType = element.GetType().Name;
                errors.Add($"Element at index {i} in list attribute '{code}' expects type '{expectedType}' but got '{actualType}'.");
            }
        }
    }

    private static string GetExpectedTypeName(AttributeType type) =>
        type switch
        {
            ScalarAttributeType scalar => scalar.DataType.ToString(),
            ComplexAttributeType => "Complex",
            ListAttributeType => "List",
            _ => type.GetType().Name
        };

    private static bool ValidateValueAgainstType(object value, AttributeType type) =>
        type switch
        {
            ScalarAttributeType scalar => ValidateScalarValue(value, scalar.DataType),
            ComplexAttributeType complexType => value is IReadOnlyDictionary<string, object> dict && ValidateComplexValue(dict, complexType),
            ListAttributeType listType => value is IReadOnlyList<object> list && list.All(el => ValidateValueAgainstType(el, listType.ElementType)),
            _ => false
        };

    private static bool ValidateComplexValue(IReadOnlyDictionary<string, object> value, ComplexAttributeType complexType)
    {
        foreach (var (key, propValue) in value)
        {
            if (!complexType.TryGetProperty(key, out _, out var prop))
            {
                return false;
            }

            if (!ValidateValueAgainstType(propValue, prop.Type))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateScalarValue(object value, ScalarDataType dataType) =>
        dataType switch
        {
            ScalarDataType.Boolean => value is bool,
            ScalarDataType.Date => value is DateOnly,
            ScalarDataType.DateTime => value is DateTimeOffset,
            ScalarDataType.Decimal => value is decimal,
            ScalarDataType.Integer => value is int,
            ScalarDataType.String => value is string,
            _ => false
        };

    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes) => new(attributes, []);

    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes, IEnumerable<AttributeGroup> groups) => new(attributes, groups);
}
