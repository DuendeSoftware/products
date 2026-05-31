// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Maps SCIM request bodies to domain objects.
/// </summary>
internal static class ScimRequestMapper
{

    // Known top-level SCIM fields that are not schema attributes
    private static readonly HashSet<string> KnownTopLevelFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "schemas", "id", "externalid", "username", "meta", "password"
        };

    /// <summary>
    /// Result of mapping a SCIM user request to domain attributes.
    /// </summary>
    internal sealed class MappingResult
    {
        private MappingResult() { }

        internal bool IsSuccess { get; private set; }
        internal string? ErrorDetail { get; private set; }
        internal string? ErrorScimType { get; private set; }
        internal ValidatedAttributeValueCollection? Attributes { get; private set; }
        internal string? Password { get; private set; }

        internal static MappingResult Success(
            ValidatedAttributeValueCollection attributes,
            string? password) =>
            new() { IsSuccess = true, Attributes = attributes, Password = password };
        internal static MappingResult Failure(string detail) =>
            new() { IsSuccess = false, ErrorDetail = detail };

        internal static MappingResult Failure(string detail, string scimType) =>
            new() { IsSuccess = false, ErrorDetail = detail, ErrorScimType = scimType };
    }

    /// <summary>
    /// Maps a <see cref="ScimUserRequest"/> to domain objects.
    /// Returns an error result if validation fails or attributes cannot be mapped.
    /// </summary>
    internal static MappingResult Map(ScimUserRequest request, AttributeSchema? schema)
    {
        // Build the attribute collection
        var collection = schema is not null
            ? new AttributeValueCollection(schema)
            : new AttributeValueCollection(AttributeSchema.Empty);

        var userName = request.UserName;
        if (userName is not null)
        {
            if (schema is null)
            {
                return MappingResult.Failure(
                    "userName is not defined in the schema.",
                    ScimConstants.ErrorTypes.InvalidValue);
            }

            if (AttributeCode.TryCreate(ScimConstants.UserNameAttributeName, out var userNameCode))
            {
                if (schema.AttributeDefinitions.TryGetValue(userNameCode, out var userNameDef))
                {
                    if (!userNameDef.IsUnique)
                    {
                        return MappingResult.Failure(
                            "userName attribute must be configured as unique.",
                            ScimConstants.ErrorTypes.InvalidValue);
                    }

                    collection.Set(userNameCode, userName);
                }
                else
                {
                    return MappingResult.Failure(
                        "userName is not defined in the schema.",
                        ScimConstants.ErrorTypes.InvalidValue);
                }
            }
        }

        // Handle externalId — store as schema attribute "externalid"
        var externalId = request.ExternalId;
        if (externalId is not null && schema is not null)
        {
            if (AttributeCode.TryCreate(ScimConstants.ExternalIdAttributeName, out var extIdName))
            {
                if (schema.AttributeDefinitions.ContainsKey(extIdName))
                {
                    collection.Set(extIdName, externalId);
                }
            }
        }

        // Map additional JSON attributes to schema attributes
        if (request.AdditionalAttributes is not null && schema is not null)
        {
            foreach (var kvp in request.AdditionalAttributes)
            {
                // Skip known top-level fields
                if (KnownTopLevelFields.Contains(kvp.Key))
                {
                    continue;
                }

                if (!AttributeCode.TryCreate(kvp.Key, out var attrName))
                {
                    return MappingResult.Failure(
                        $"Unknown or invalid attribute name: '{kvp.Key}'.",
                        ScimConstants.ErrorTypes.InvalidPath);
                }

                if (!schema.AttributeDefinitions.TryGetValue(attrName, out var definition))
                {
                    return MappingResult.Failure(
                        $"Attribute '{kvp.Key}' is not defined in the schema.",
                        ScimConstants.ErrorTypes.InvalidPath);
                }

                var mapResult = MapJsonElement(kvp.Value, definition, definition, collection);
                if (mapResult is not null)
                {
                    return mapResult;
                }
            }
        }
        else if (request.AdditionalAttributes is not null && schema is null)
        {
            // No schema registered; extra attributes not allowed
            var firstUnknown = request.AdditionalAttributes.Keys
                .FirstOrDefault(k => !KnownTopLevelFields.Contains(k));

            if (firstUnknown is not null)
            {
                return MappingResult.Failure(
                    $"Attribute '{firstUnknown}' is not defined in the schema.",
                    ScimConstants.ErrorTypes.InvalidPath);
            }
        }

        return MappingResult.Success(collection.Validate(), request.Password);
    }

    private static MappingResult? MapJsonElement(
        JsonElement element,
        AttributeDefinition definition,
        AttributeCode attrCode,
        AttributeValueCollection collection)
    {
        try
        {
            var set = TrySetJsonElement(element, definition, attrCode, collection);
            if (!set)
            {
                return MappingResult.Failure(
                    $"Invalid value type for attribute '{attrCode}': expected {definition.AttributeType.GetType().Name}.",
                    ScimConstants.ErrorTypes.InvalidValue);
            }
        }
        catch (ArgumentException)
        {
            return MappingResult.Failure(
                $"Invalid value for attribute '{attrCode}'.",
                ScimConstants.ErrorTypes.InvalidValue);
        }

        return null; // no error
    }

    /// <summary>
    /// Sets a <see cref="JsonElement"/> value on <paramref name="collection"/> for the given attribute.
    /// Returns false if the element's JSON value kind does not match the expected type.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the collection rejects the value.</exception>
    internal static bool TrySetJsonElement(
        JsonElement element,
        AttributeDefinition definition,
        AttributeCode attrCode,
        AttributeValueCollection collection)
    {
        switch (definition.AttributeType)
        {
            case ComplexAttributeType complexType:
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }
                var dict = ConvertComplexJsonToDict(element, complexType);
                if (dict is null)
                {
                    return false;
                }
                collection.Set(attrCode, (IReadOnlyDictionary<string, object>)dict);
                return true;

            case ListAttributeType listType:
                if (element.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }
                var list = ConvertListJsonToList(element, listType);
                if (list is null)
                {
                    return false;
                }
                collection.Set(attrCode, (IReadOnlyList<object>)list);
                return true;

            case ScalarAttributeType scalarType:
                return TrySetScalarJsonElement(element, scalarType.DataType, attrCode, collection);

            default:
                return false;
        }
    }

    private static bool TrySetScalarJsonElement(
        JsonElement element,
        ScalarDataType dataType,
        AttributeCode attrCode,
        AttributeValueCollection collection)
    {
        switch (dataType)
        {
            case ScalarDataType.Boolean when element.ValueKind == JsonValueKind.True:
                collection.Set(attrCode, true);
                return true;
            case ScalarDataType.Boolean when element.ValueKind == JsonValueKind.False:
                collection.Set(attrCode, false);
                return true;
            case ScalarDataType.Integer when element.ValueKind == JsonValueKind.Number:
                if (!element.TryGetInt32(out var i))
                {
                    return false;
                }
                collection.Set(attrCode, i);
                return true;
            case ScalarDataType.Decimal when element.ValueKind == JsonValueKind.Number:
                if (!element.TryGetDecimal(out var dec))
                {
                    return false;
                }
                collection.Set(attrCode, dec);
                return true;
            case ScalarDataType.String when element.ValueKind == JsonValueKind.String:
                collection.Set(attrCode, element.GetString()!);
                return true;
            case ScalarDataType.Date when element.ValueKind == JsonValueKind.String:
                if (!DateOnly.TryParse(element.GetString(), out var d))
                {
                    return false;
                }
                collection.Set(attrCode, d);
                return true;
            case ScalarDataType.DateTime when element.ValueKind == JsonValueKind.String:
                if (!DateTimeOffset.TryParse(element.GetString(), out var dt))
                {
                    return false;
                }
                collection.Set(attrCode, dt);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Converts a JSON object element to a <see cref="Dictionary{TKey,TValue}"/> for a <see cref="ComplexAttributeType"/>.
    /// Property names are compared case-insensitively. Returns null on type mismatch.
    /// </summary>
    private static Dictionary<string, object>? ConvertComplexJsonToDict(
        JsonElement element,
        ComplexAttributeType complexType)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in element.EnumerateObject())
        {
            if (!complexType.TryGetProperty(prop.Name, out var canonicalKey, out var complexProp))
            {
                // Unknown sub-property — return null to signal type mismatch (triggers 400)
                return null;
            }

            var converted = ConvertJsonValueByType(prop.Value, complexProp.Type);
            if (converted is null)
            {
                return null;
            }

            result[canonicalKey!.Value] = converted;
        }

        return result;
    }

    /// <summary>
    /// Converts a JSON array element to a <see cref="List{T}"/> for a <see cref="ListAttributeType"/>.
    /// Returns null on element type mismatch.
    /// </summary>
    private static List<object>? ConvertListJsonToList(
        JsonElement element,
        ListAttributeType listType)
    {
        var result = new List<object>();

        foreach (var item in element.EnumerateArray())
        {
            var converted = ConvertJsonValueByType(item, listType.ElementType);
            if (converted is null)
            {
                return null;
            }

            result.Add(converted);
        }

        return result;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to the CLR value appropriate for the given <see cref="AttributeType"/>.
    /// Returns null on type mismatch.
    /// </summary>
    private static object? ConvertJsonValueByType(JsonElement element, AttributeType attributeType) =>
        attributeType switch
        {
            ComplexAttributeType complexType when element.ValueKind == JsonValueKind.Object =>
                ConvertComplexJsonToDict(element, complexType),

            ListAttributeType listType when element.ValueKind == JsonValueKind.Array =>
                ConvertListJsonToList(element, listType),

            ScalarAttributeType scalarType => ConvertScalarJsonValue(element, scalarType.DataType),

            _ => null
        };

    private static object? ConvertScalarJsonValue(JsonElement element, ScalarDataType dataType) =>
        dataType switch
        {
            ScalarDataType.Boolean when element.ValueKind == JsonValueKind.True => (object)true,
            ScalarDataType.Boolean when element.ValueKind == JsonValueKind.False => false,
            ScalarDataType.Integer when element.ValueKind == JsonValueKind.Number =>
                element.TryGetInt32(out var i) ? (object)i : null,
            ScalarDataType.Decimal when element.ValueKind == JsonValueKind.Number =>
                element.TryGetDecimal(out var dec) ? (object)dec : null,
            ScalarDataType.String when element.ValueKind == JsonValueKind.String => element.GetString()!,
            ScalarDataType.Date when element.ValueKind == JsonValueKind.String =>
                DateOnly.TryParse(element.GetString(), out var d) ? (object)d : null,
            ScalarDataType.DateTime when element.ValueKind == JsonValueKind.String =>
                DateTimeOffset.TryParse(element.GetString(), out var dt) ? (object)dt : null,
            _ => null
        };
}
