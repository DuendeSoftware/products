// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal.Storage;

namespace Duende.UserManagement.Profiles;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class AttributeSchemaRepository(IStoreFactory storeFactory)
{
    // Attribute schemas are configuration data.
    internal async Task<CreateResult> CreateAsync(UuidV7 schemaId, AttributeSchema schema, Ct ct)
    {
        var store = storeFactory.GetStore();
        return await store.CreateAsync(schemaId, ToDso(schema), [], [], Expiration.NoExpiration, [], ct);
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 schemaId, AttributeSchema schema, int expectedVersion, Ct ct)
    {
        var store = storeFactory.GetStore();
        return await store.UpdateAsync(schemaId, ToDso(schema), expectedVersion, [], [], expiration: Expiration.NoExpiration, [], ct);
    }

    internal async Task<(AttributeSchema AttributeSchema, int Version)?> TryReadAsync(UuidV7 schemaId, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(AttributeSchemaDso.EntityType, schemaId, ct);
        return result.Found
            ? (ToEntity(result.Dso, schemaId, result.Version.Value), result.Version.Value)
            : null;
    }

    private static AttributeSchema ToEntity(IDataStorageObject value, UuidV7 schemaId, int version) =>
        value switch
        {
            AttributeSchemaDso.V1 v1 => ToEntity(v1, schemaId, version),
            _ => throw new InvalidOperationException($"Unexpected type: {value.GetType().Name}")
        };

    private static AttributeSchemaDso.V1 ToDso(AttributeSchema entity) =>
        new([.. entity.AttributeDefinitions.Values.Select(ToDso)],
            [.. entity.Groups.Values.Select(ToDso)]);

    private static AttributeGroupDso.V1 ToDso(AttributeGroup group) =>
        new(group.Code.Value, group.DisplayName?.Value, group.Description?.Value, group.Order);

    private static AttributeDefinitionDso.V1 ToDso(AttributeDefinition vo) =>
        new(vo.Code.Value, ToTypeDso(vo.AttributeType), vo.Description?.Value, vo.IsUnique, [.. vo.Tags],
            vo.GroupCode?.Value, vo.Order, vo.DisplayName?.Value, vo.IsQueryable, vo.IsRequired);

    private static AttributeTypeDso ToTypeDso(AttributeType type) =>
        type switch
        {
            ScalarAttributeType scalar => new AttributeTypeDso(
                "Scalar", scalar.DataType.ToString(), null, null, null, null),

            ComplexAttributeType complex => new AttributeTypeDso(
                "Complex", null, null, null,
                complex.Properties.ToDictionary(
                    kvp => kvp.Key.Value,
                    kvp => new ComplexPropertyDso(ToTypeDso(kvp.Value.Type), kvp.Value.DisplayName?.Value, kvp.Value.Description?.Value)),
                null),

            ListAttributeType list => new AttributeTypeDso(
                "List", null, null, null, null, ToTypeDso(list.ElementType)),

            _ => throw new InvalidOperationException($"Unknown AttributeType: {type.GetType().Name}")
        };

    private static AttributeSchema ToEntity(AttributeSchemaDso.V1 dso, UuidV7 schemaId, int version)
    {
        var groups = (dso.Groups ?? []).Select(g => new AttributeGroup(
            AttributeGroupCode.Load(g.Code),
            g.DisplayName is not null ? AttributeDisplayName.Load(g.DisplayName) : null,
            g.Description is not null ? AttributeDescription.Load(g.Description) : null,
            g.Order));

        var attributes = dso.AttributeDefinitions.Select(att => AttributeDefinition.Load(
            AttributeCode.Load(att.Code),
            ToTypeValueObject(att.Type),
            att.Description is not null ? AttributeDescription.Load(att.Description) : null,
            att.DisplayName is not null ? AttributeDisplayName.Load(att.DisplayName) : null,
            att.IsUnique,
            att.IsQueryable,
            att.IsRequired,
            att.Tags,
            att.GroupCode is not null ? AttributeGroupCode.Load(att.GroupCode) : null,
            att.Order));

        return AttributeSchema.Load(attributes, groups, schemaId, version);
    }

    private static AttributeType ToTypeValueObject(AttributeTypeDso dso) =>
        dso.Kind switch
        {
            "Scalar" => new ScalarAttributeType(Enum.Parse<ScalarDataType>(dso.ScalarDataType!)),

            "Complex" => new ComplexAttributeType(
                (dso.Properties ?? []).ToDictionary(
                    kvp => AttributeCode.Load(kvp.Key),
                    kvp => ComplexAttributeProperty.Of(
                        ToTypeValueObject(kvp.Value.Type),
                        kvp.Value.DisplayName is not null ? AttributeDisplayName.Load(kvp.Value.DisplayName) : null,
                        kvp.Value.Description is not null ? AttributeDescription.Load(kvp.Value.Description) : null))),

            "List" => new ListAttributeType(ToTypeValueObject(dso.ElementType!)),

            _ => throw new InvalidOperationException($"Unknown AttributeTypeDso kind: {dso.Kind}")
        };
}
