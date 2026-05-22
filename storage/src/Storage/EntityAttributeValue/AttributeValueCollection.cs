// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Duende.Storage.EntityAttributeValue.Internal;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     A mutable collection of attribute values validated against an attribute schema.
/// </summary>
public sealed class AttributeValueCollection : IEnumerable<AttributeValue>
{
    private readonly Dictionary<AttributeCode, AttributeValue> _dict = [];
    private readonly IReadOnlyAttributeSchema? _schema;

    /// <summary>
    ///     Creates an empty collection backed by the specified schema.
    /// </summary>
    /// <param name="schema">The attribute schema used for validation.</param>
    public AttributeValueCollection(IReadOnlyAttributeSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schema = schema;
    }

    /// <summary>
    ///     Creates a collection pre-populated with the specified attributes, validated against the schema.
    /// </summary>
    /// <param name="schema">The attribute schema used for validation.</param>
    /// <param name="attributes">The initial attribute values to populate.</param>
    public AttributeValueCollection(IReadOnlyAttributeSchema schema, IEnumerable<AttributeValue> attributes)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(attributes);
        _schema = schema;

        foreach (var attribute in attributes)
        {
            if (!_schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition))
            {
                throw new ArgumentException(
                    $"Attribute '{attribute.Code}' is not defined in the schema.", nameof(attributes));
            }

            if (!AttributeTypeMatchesValue(definition, attribute))
            {
                throw new ArgumentException(
                    $"Attribute '{attribute.Code}' has a value of the wrong type.", nameof(attributes));
            }

            if (!_dict.TryAdd(attribute.Code, attribute))
            {
                throw new ArgumentException(
                    $"The attributes contain more than one attribute named '{attribute.Code}'", nameof(attributes));
            }
        }
    }

    /// <summary>
    ///     Sets an attribute value in the collection, replacing any existing value for the same code.
    /// </summary>
    /// <param name="attribute">The attribute value to set.</param>
    public void Set(AttributeValue attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (_schema != null)
        {
            if (!_schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition))
            {
                throw new ArgumentException(
                    $"Attribute '{attribute.Code}' is not defined in the schema.", nameof(attribute));
            }

            if (!AttributeTypeMatchesValue(definition, attribute))
            {
                throw new ArgumentException(
                    $"Attribute '{attribute.Code}' has a value of the wrong type.", nameof(attribute));
            }
        }

        _dict[attribute.Code] = attribute;
    }

    /// <summary>Sets a boolean attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The boolean value.</param>
    public void Set(AttributeCode code, bool value) =>
        SetTyped(code, value, ScalarDataType.Boolean);

    /// <summary>Sets an integer attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The integer value.</param>
    public void Set(AttributeCode code, int value) =>
        SetTyped(code, value, ScalarDataType.Integer);

    /// <summary>Sets a decimal attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The decimal value.</param>
    public void Set(AttributeCode code, decimal value) =>
        SetTyped(code, value, ScalarDataType.Decimal);

    /// <summary>Sets a string attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The string value.</param>
    public void Set(AttributeCode code, string value) =>
        SetTyped(code, value, ScalarDataType.String);

    /// <summary>Sets a date attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The date value.</param>
    public void Set(AttributeCode code, DateOnly value) =>
        SetTyped(code, value, ScalarDataType.Date);

    /// <summary>Sets a date-time attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The date-time value.</param>
    public void Set(AttributeCode code, DateTimeOffset value) =>
        SetTyped(code, value, ScalarDataType.DateTime);

    /// <summary>Sets a complex (dictionary) attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The complex value as a dictionary of properties.</param>
    public void Set(AttributeCode code, IReadOnlyDictionary<string, object> value)
    {
        if (_schema != null)
        {
            ValidateComplexAgainstSchema(code, value);
        }

        _dict[code] = new AttributeValue<IReadOnlyDictionary<string, object>>(code, value);
    }

    /// <summary>Sets a list attribute value.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The list value.</param>
    public void Set(AttributeCode code, IReadOnlyList<object> value)
    {
        if (_schema != null)
        {
            ValidateListAgainstSchema(code, value);
        }

        _dict[code] = new AttributeValue<IReadOnlyList<object>>(code, value);
    }

    /// <summary>Attempts to set a boolean value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, bool value, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TrySetTyped(code, value, ScalarDataType.Boolean, out errors);

    /// <summary>Attempts to set an integer value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The integer value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, int value, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TrySetTyped(code, value, ScalarDataType.Integer, out errors);

    /// <summary>Attempts to set a decimal value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The decimal value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, decimal value, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TrySetTyped(code, value, ScalarDataType.Decimal, out errors);

    /// <summary>Attempts to set a string value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The string value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, string value, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TrySetTyped(code, value, ScalarDataType.String, out errors);

    /// <summary>Attempts to set a date value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The date value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, DateOnly value, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TrySetTyped(code, value, ScalarDataType.Date, out errors);

    /// <summary>Attempts to set a date-time value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The date-time value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, DateTimeOffset value, [NotNullWhen(false)] out IReadOnlyList<string>? errors) =>
        TrySetTyped(code, value, ScalarDataType.DateTime, out errors);

    /// <summary>Attempts to set a complex (dictionary) value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The complex value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, IReadOnlyDictionary<string, object> value, [NotNullWhen(false)] out IReadOnlyList<string>? errors)
    {
        if (_schema != null)
        {
            var errorList = new List<string>();
            TryCollectComplexErrors(code, value, _schema, errorList);
            if (errorList.Count > 0)
            {
                errors = errorList;
                return false;
            }
        }

        _dict[code] = new AttributeValue<IReadOnlyDictionary<string, object>>(code, value);
        errors = null;
        return true;
    }

    /// <summary>Attempts to set a list value, returning errors on failure.</summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The list value.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
    public bool TrySet(AttributeCode code, IReadOnlyList<object> value, [NotNullWhen(false)] out IReadOnlyList<string>? errors)
    {
        if (_schema != null)
        {
            var errorList = new List<string>();
            TryCollectListErrors(code, value, _schema, errorList);
            if (errorList.Count > 0)
            {
                errors = errorList;
                return false;
            }
        }

        _dict[code] = new AttributeValue<IReadOnlyList<object>>(code, value);
        errors = null;
        return true;
    }

    /// <summary>
    ///     Validates the collection against the schema and returns an immutable validated collection.
    /// </summary>
    /// <returns>A validated, immutable attribute value collection.</returns>
    public ValidatedAttributeValueCollection Validate()
    {
        if (_schema == null)
        {
            throw new InvalidOperationException("Cannot validate without a schema. Use the constructor that accepts an IReadOnlyAttributeSchema.");
        }

        var missing = _schema.AttributeDefinitions.Values
            .Where(d => d.IsRequired && !_dict.ContainsKey(d.Code))
            .Select(d => $"Required attribute '{d.Code}' is missing.")
            .ToList();

        if (missing.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", missing));
        }

        var schema = (AttributeSchema)_schema;
        return new ValidatedAttributeValueCollection(_dict.Values, schema.SchemaId, schema.Version);
    }

    /// <summary>
    ///     Attempts to validate the collection, returning the validated collection or errors.
    /// </summary>
    /// <param name="validated">The validated collection if successful.</param>
    /// <param name="errors">Validation errors if the operation fails.</param>
    /// <returns><c>true</c> if validation succeeds; otherwise, <c>false</c>.</returns>
    public bool TryValidate([NotNullWhen(true)] out ValidatedAttributeValueCollection? validated, [NotNullWhen(false)] out IReadOnlyList<string>? errors)
    {
        if (_schema == null)
        {
            validated = null;
            errors = ["Cannot validate without a schema. Use the constructor that accepts an IReadOnlyAttributeSchema."];
            return false;
        }

        var missing = _schema.AttributeDefinitions.Values
            .Where(d => d.IsRequired && !_dict.ContainsKey(d.Code))
            .Select(d => $"Required attribute '{d.Code}' is missing.")
            .ToList();

        if (missing.Count > 0)
        {
            validated = null;
            errors = missing;
            return false;
        }

        var schema = (AttributeSchema)_schema;
        validated = new ValidatedAttributeValueCollection(_dict.Values, schema.SchemaId, schema.Version);
        errors = null;
        return true;
    }

    /// <summary>
    ///     Gets the number of attribute values in the collection.
    /// </summary>
    public int Count => _dict.Count;

    /// <summary>
    ///     Removes the attribute value with the specified code.
    /// </summary>
    /// <param name="code">The attribute code to remove.</param>
    /// <returns><c>true</c> if the attribute was removed; otherwise, <c>false</c>.</returns>
    public bool Remove(AttributeCode code)
    {
        if (_schema != null &&
            _schema.AttributeDefinitions.TryGetValue(code, out var def) &&
            def.IsRequired)
        {
            throw new InvalidOperationException($"Cannot remove required attribute '{code}'.");
        }

        return _dict.Remove(code);
    }

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
    public IEnumerator<AttributeValue> GetEnumerator() => _dict.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void SetTyped<T>(AttributeCode code, T value, ScalarDataType expectedType)
    {
        if (_schema != null)
        {
            ValidateScalarAgainstSchema(code, expectedType);
        }

        _dict[code] = new AttributeValue<T>(code, value);
    }

    private bool TrySetTyped<T>(AttributeCode code, T value, ScalarDataType expectedType, out IReadOnlyList<string>? errors)
    {
        if (_schema != null)
        {
            if (!_schema.AttributeDefinitions.TryGetValue(code, out var definition))
            {
                errors = [$"Attribute '{code}' is not defined in the schema."];
                return false;
            }

            if (definition.AttributeType is not ScalarAttributeType scalar || scalar.DataType != expectedType)
            {
                var providedType = typeof(T).Name;
                var definedType = definition.AttributeType is ScalarAttributeType s ? s.DataType.ToString() : definition.AttributeType.GetType().Name;
                errors = [$"Attribute '{code}' is defined as '{definedType}' but a '{providedType}' value was provided."];
                return false;
            }
        }

        _dict[code] = new AttributeValue<T>(code, value);
        errors = null;
        return true;
    }

    private void ValidateScalarAgainstSchema(AttributeCode code, ScalarDataType expectedType)
    {
        if (!_schema!.AttributeDefinitions.TryGetValue(code, out var definition))
        {
            throw new ArgumentException($"Attribute '{code}' is not defined in the schema.");
        }

        if (definition.AttributeType is not ScalarAttributeType scalar || scalar.DataType != expectedType)
        {
            var definedType = definition.AttributeType is ScalarAttributeType s ? s.DataType.ToString() : definition.AttributeType.GetType().Name;
            throw new ArgumentException($"Attribute '{code}' is defined as '{definedType}' but a value of type '{expectedType}' was provided.");
        }
    }

    private void ValidateComplexAgainstSchema(AttributeCode code, IReadOnlyDictionary<string, object> value)
    {
        var errorList = new List<string>();
        TryCollectComplexErrors(code, value, _schema!, errorList);
        if (errorList.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errorList));
        }
    }

    private void ValidateListAgainstSchema(AttributeCode code, IReadOnlyList<object> value)
    {
        var errorList = new List<string>();
        TryCollectListErrors(code, value, _schema!, errorList);
        if (errorList.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errorList));
        }
    }

    private static void TryCollectComplexErrors(AttributeCode code, IReadOnlyDictionary<string, object> value, IReadOnlyAttributeSchema schema, List<string> errors)
    {
        if (!schema.AttributeDefinitions.TryGetValue(code, out var definition))
        {
            errors.Add($"Attribute '{code}' is not defined in the schema.");
            return;
        }

        if (definition.AttributeType is not ComplexAttributeType complexType)
        {
            errors.Add($"Attribute '{code}' is not a complex type.");
            return;
        }

        CollectComplexValueErrors(code, value, complexType, errors);
    }

    private static void TryCollectListErrors(AttributeCode code, IReadOnlyList<object> value, IReadOnlyAttributeSchema schema, List<string> errors)
    {
        if (!schema.AttributeDefinitions.TryGetValue(code, out var definition))
        {
            errors.Add($"Attribute '{code}' is not defined in the schema.");
            return;
        }

        if (definition.AttributeType is not ListAttributeType listType)
        {
            errors.Add($"Attribute '{code}' is not a list type.");
            return;
        }

        CollectListValueErrors(code, value, listType, errors);
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

    private static bool AttributeTypeMatchesValue(AttributeDefinition definition, AttributeValue attribute) =>
        definition.AttributeType switch
        {
            ScalarAttributeType scalar => scalar.DataType switch
            {
                ScalarDataType.Boolean => attribute is AttributeValue<bool>,
                ScalarDataType.Date => attribute is AttributeValue<DateOnly>,
                ScalarDataType.DateTime => attribute is AttributeValue<DateTimeOffset>,
                ScalarDataType.Decimal => attribute is AttributeValue<decimal>,
                ScalarDataType.Integer => attribute is AttributeValue<int>,
                ScalarDataType.String => attribute is AttributeValue<string>,
                _ => false
            },
            ComplexAttributeType => attribute is AttributeValue<IReadOnlyDictionary<string, object>>,
            ListAttributeType => attribute is AttributeValue<IReadOnlyList<object>>,
            _ => false
        };
}
