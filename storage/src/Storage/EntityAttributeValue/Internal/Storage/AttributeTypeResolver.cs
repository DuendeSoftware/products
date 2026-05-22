// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Resolves SCIM attribute paths to Faro Field types based on the dynamic user schema.
/// Unlike other <see cref="IScimAttributeTypeResolver"/> implementations which map fixed SCIM User schema attributes,
/// this resolver dynamically maps user-defined schema attributes to their Faro field types.
/// Supports dotted paths (e.g., <c>address.city</c>, <c>phones.type</c>) for complex and list types.
/// </summary>
public sealed class AttributeTypeResolver : IScimAttributeTypeResolver
{
    private readonly IReadOnlyDictionary<AttributeCode, AttributeDefinition> _attributeDefinitions;

    /// <summary>
    /// Creates a new resolver with the given user schema attribute definitions.
    /// </summary>
    /// <param name="attributeDefinitions">The schema attribute definitions for the user schema.</param>
    public AttributeTypeResolver(
        IReadOnlyDictionary<AttributeCode, AttributeDefinition> attributeDefinitions) =>
        _attributeDefinitions = attributeDefinitions ?? throw new ArgumentNullException(nameof(attributeDefinitions));

    /// <inheritdoc />
    public Field ResolveField(string attributePath)
    {
        var normalized = attributePath.Trim();

        // Handle built-in SCIM User fields that are stored as first-class columns
        if (string.Equals(normalized, "username", StringComparison.OrdinalIgnoreCase))
        {
            return new StringField("userName");
        }

        // Handle system timestamp fields
        if (normalized == SystemFields.CreatedAttributeName)
        {
            return SystemFields.CreatedAtField;
        }
        if (normalized == SystemFields.LastUpdatedAttributeName)
        {
            return SystemFields.LastUpdatedAtField;
        }

        // Split on '.' to handle dotted paths (e.g., "address.city", "phones.type")
        var segments = normalized.Split('.');
        var rootSegment = segments[0];

        // Resolve the root attribute from dynamic schema
        if (!AttributeCode.TryCreate(rootSegment, out var schemaCode) ||
            !_attributeDefinitions.TryGetValue(schemaCode, out var definition))
        {
            throw new NotSupportedException($"Unknown user attribute: {attributePath}");
        }

        if (!definition.IsQueryable)
        {
            throw new NotSupportedException(
                $"Attribute '{attributePath}' is not queryable and cannot be used in filter or sort expressions.");
        }

        // Walk the remaining segments through the type tree
        var isArray = false;
        var currentType = definition.AttributeType;

        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];

            // Unwrap any list wrapper before navigating into the segment
            if (currentType is ListAttributeType listWrapper)
            {
                isArray = true;
                currentType = listWrapper.ElementType;
            }

            switch (currentType)
            {
                case ComplexAttributeType complex:
                    if (!complex.TryGetProperty(segment, out _, out var prop))
                    {
                        throw new NotSupportedException(
                            $"Unknown property '{segment}' on complex attribute '{attributePath}'.");
                    }
                    currentType = prop.Type;
                    break;

                default:
                    throw new NotSupportedException(
                        $"Cannot navigate into segment '{segment}' of type '{currentType.GetType().Name}' in path '{attributePath}'.");
            }
        }

        // Unwrap any trailing list wrapper (e.g., root type is List<scalar> with no sub-path).
        // For a root list attribute (e.g., "tags" where tags: List<string>), we resolve to a
        // multi-valued field so the evaluator checks all array-indexed entries.
        if (currentType is ListAttributeType trailingList)
        {
            isArray = true;
            currentType = trailingList.ElementType;
        }

        return MapToField(currentType, normalized, isArray);
    }

    private static Field MapToField(AttributeType type, string fullPath, bool isArray) =>
        type switch
        {
            ScalarAttributeType scalar => MapScalarToField(scalar.DataType, fullPath, isArray),

            ComplexAttributeType => throw new NotSupportedException(
                $"Cannot query a complex attribute directly at path '{fullPath}'. Use a sub-property path."),

            ListAttributeType => throw new NotSupportedException(
                $"Cannot query a list attribute directly at path '{fullPath}'. Use a sub-property path."),

            _ => throw new NotSupportedException($"Unsupported schema attribute type: {type.GetType().Name}")
        };

    private static Field MapScalarToField(ScalarDataType dataType, string fullPath, bool isMultiValued) =>
        dataType switch
        {
            ScalarDataType.Boolean => new BooleanField(fullPath, isMultiValued),
            ScalarDataType.Date => new DateTimeField(fullPath, isMultiValued),
            ScalarDataType.DateTime => new DateTimeField(fullPath, isMultiValued),
            ScalarDataType.Decimal => new NumberField(fullPath, isMultiValued),
            ScalarDataType.Integer => new NumberField(fullPath, isMultiValued),
            ScalarDataType.String => new StringField(fullPath, isMultiValued),
            _ => throw new NotSupportedException(
                $"Unsupported schema attribute data type: {dataType} for path {fullPath}")
        };
}
