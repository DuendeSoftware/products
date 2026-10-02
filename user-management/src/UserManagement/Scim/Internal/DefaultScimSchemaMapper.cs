// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Default mapper that converts <see cref="AttributeDefinition"/> to
/// <see cref="ScimSchemaAttributeModel"/> using sensible SCIM defaults.
/// </summary>
internal sealed class DefaultScimSchemaMapper : IScimSchemaMapper
{
    public ScimSchemaAttributeModel Map(AttributeDefinition definition) =>
        Map(
            definition.Code.Value,
            definition.AttributeType,
            definition.Description?.ToString(),
            definition.IsRequired,
            definition.IsUnique,
            null);

    private static ScimSchemaAttributeModel Map(
        string name,
        AttributeType attributeType,
        string? description,
        bool required,
        bool unique,
        string? parentName) =>
        new()
        {
            Name = name,
            Type = MapAttributeType(name, attributeType, parentName),
            MultiValued = attributeType is ListAttributeType,
            Description = description,
            Required = required,
            CaseExact = false,
            Mutability = MapMutability(name),
            Returned = MapReturned(name),
            Uniqueness = unique ? ScimConstants.UniquenessValues.Server : ScimConstants.UniquenessValues.None,
            SubAttributes = MapSubAttributes(name, attributeType)
        };

    private static ScimSchemaAttributeModel[]? MapSubAttributes(string name, AttributeType attributeType)
    {
        var complexType = attributeType switch
        {
            ComplexAttributeType complex => complex,
            ListAttributeType { ElementType: ComplexAttributeType complex } => complex,
            _ => null
        };

        return complexType?.Properties.Select(property =>
            Map(
                property.Key.Value,
                property.Value.Type,
                property.Value.Description?.ToString(),
                false,
                false,
                name)).ToArray();
    }

    private static string MapAttributeType(string name, AttributeType attributeType, string? parentName)
    {
        if (parentName?.Equals("x509Certificates", StringComparison.OrdinalIgnoreCase) == true
            && name.Equals("value", StringComparison.OrdinalIgnoreCase))
        {
            return ScimConstants.DataTypes.Binary;
        }

        return MapAttributeType(attributeType);
    }

    private static string MapMutability(string name) =>
        name.Equals("password", StringComparison.OrdinalIgnoreCase)
            ? ScimConstants.MutabilityValues.WriteOnly
            : ScimConstants.MutabilityValues.ReadWrite;

    private static string MapReturned(string name) =>
        name.Equals("password", StringComparison.OrdinalIgnoreCase)
            ? ScimConstants.ReturnedValues.Never
            : ScimConstants.ReturnedValues.Default;

    internal static string MapAttributeType(AttributeType attributeType) =>
        attributeType switch
        {
            ScalarAttributeType scalar => MapDataType(scalar.DataType),
            ComplexAttributeType => ScimConstants.DataTypes.Complex,
            ListAttributeType list => MapAttributeType(list.ElementType),
            _ => ScimConstants.DataTypes.String
        };

    internal static string MapDataType(ScalarDataType dataType) =>
        dataType switch
        {
            ScalarDataType.Boolean => ScimConstants.DataTypes.Boolean,
            ScalarDataType.Date => ScimConstants.DataTypes.DateTime,
            ScalarDataType.DateTime => ScimConstants.DataTypes.DateTime,
            ScalarDataType.Decimal => ScimConstants.DataTypes.Decimal,
            ScalarDataType.Integer => ScimConstants.DataTypes.Integer,
            ScalarDataType.String => ScimConstants.DataTypes.String,
            _ => ScimConstants.DataTypes.String
        };
}
