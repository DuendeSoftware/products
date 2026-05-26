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
        new()
        {
            Name = definition.Code.ToString(),
            Type = MapAttributeType(definition.AttributeType),
            MultiValued = definition.AttributeType is ListAttributeType,
            Description = definition.Description?.ToString(),
            Required = false,
            CaseExact = definition.AttributeType is ScalarAttributeType scalar
                && scalar.DataType != ScalarDataType.String,
            Mutability = ScimConstants.MutabilityValues.ReadWrite,
            Returned = ScimConstants.ReturnedValues.Default,
            Uniqueness = definition.IsUnique ? ScimConstants.UniquenessValues.Server : ScimConstants.UniquenessValues.None
        };

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
