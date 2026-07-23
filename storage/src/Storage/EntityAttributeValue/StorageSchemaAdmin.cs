// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     A storage-backed implementation of <see cref="ISchemaStore"/> and <see cref="ISchemaAdmin"/>
///     that persists schemas to the database via the storage layer.
/// </summary>
public sealed class StorageSchemaAdmin : ISchemaStore, ISchemaAdmin
{
    private readonly IStoreFactory _storeFactory;

    /// <summary>
    ///     Registers <see cref="StorageSchemaAdmin"/> as both <see cref="ISchemaStore"/> and
    ///     <see cref="ISchemaAdmin"/> in the service collection, including DSO registration.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    public static void RegisterServices(IServiceCollection services)
    {
        services.AddDsoRegistration<AttributeSchemaDso.V1>();
        _ = services.RemoveAll<ISchemaStore>();
        _ = services.RemoveAll<ISchemaAdmin>();
        _ = services.AddSingleton<StorageSchemaAdmin>();
        _ = services.AddSingleton<ISchemaStore>(sp => sp.GetRequiredService<StorageSchemaAdmin>());
        _ = services.AddSingleton<ISchemaAdmin>(sp => sp.GetRequiredService<StorageSchemaAdmin>());
    }

    /// <summary>
    ///     Initialises a new <see cref="StorageSchemaAdmin"/>.
    /// </summary>
    /// <param name="storeFactory">The store factory for obtaining a scoped store.</param>
    public StorageSchemaAdmin(IStoreFactory storeFactory) =>
        _storeFactory = storeFactory;

    /// <inheritdoc/>
    public async Task<IReadOnlyAttributeSchema?> GetAsync(SchemaId schemaId, CancellationToken ct)
    {
        var store = await _storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!result.Found)
        {
            return null;
        }

        var dso = (AttributeSchemaDso.V1)result.Dso;
        var config = ToConfiguration(schemaId, dso);
        return SchemaConfigurationMapper.ToReadOnlySchema(config);
    }

    /// <inheritdoc/>
    async Task<SchemaSaveResult> ISchemaAdmin.CreateAsync(SchemaConfiguration schema, CancellationToken ct)
    {
        var store = await _storeFactory.GetStore(ct);
        var id = UuidV7.New();
        var createResult = await store.CreateAsync(
            id,
            ToDso(schema),
            [DataStorageKey.Create(SchemaIdDskV1.Create(schema.SchemaId))],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration,
            [],
            ct);

        return createResult switch
        {
            CreateResult.Success => SchemaSaveResult.Success(schema.SchemaId, 1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                SchemaSaveResult.Failure(SchemaError.AlreadyExists(schema.SchemaId.ToString())),
            CreateResult.ConcurrencyConflict => SchemaSaveResult.Failure(SchemaError.VersionConflict()),
            _ => SchemaSaveResult.Failure(SchemaError.ValidationFailed("Failed to create schema."))
        };
    }

    /// <inheritdoc/>
    async Task<SchemaGetResult> ISchemaAdmin.GetAsync(SchemaId schemaId, CancellationToken ct)
    {
        var store = await _storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!result.Found)
        {
            return SchemaGetResult.NotFound();
        }

        var config = ToConfiguration(schemaId, (AttributeSchemaDso.V1)result.Dso);
        return SchemaGetResult.Ok(config, result.Version.Value);
    }

    /// <inheritdoc/>
    public async Task<SchemaSaveResult> UpdateAsync(
        SchemaId schemaId,
        SchemaConfiguration schema,
        int expectedVersion,
        CancellationToken ct)
    {
        if (schemaId != schema.SchemaId)
        {
            return SchemaSaveResult.Failure(
                SchemaError.ValidationFailed("Schema ID in the body must match the route schema ID."));
        }

        var store = await _storeFactory.GetStore(ct);
        var existing = await store.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!existing.Found)
        {
            return SchemaSaveResult.Failure(SchemaError.NotFound(schemaId.ToString()));
        }

        var updateResult = await store.UpdateAsync(
            existing.Id,
            ToDso(schema),
            expectedVersion,
            [DataStorageKey.Create(SchemaIdDskV1.Create(schema.SchemaId))],
            SearchFieldCollection.Empty,
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

        return updateResult switch
        {
            UpdateResult.Success => SchemaSaveResult.Success(schema.SchemaId, expectedVersion + 1),
            UpdateResult.UnexpectedVersion => SchemaSaveResult.Failure(SchemaError.VersionConflict()),
            UpdateResult.KeyConflict => SchemaSaveResult.Failure(SchemaError.AlreadyExists(schema.SchemaId.ToString())),
            _ => SchemaSaveResult.Failure(SchemaError.NotFound(schemaId.ToString()))
        };
    }

    /// <inheritdoc/>
    public async Task<SchemaSaveResult> DeleteAsync(SchemaId schemaId, CancellationToken ct)
    {
        var store = await _storeFactory.GetStore(ct);

        var existing = await store.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!existing.Found)
        {
            return SchemaSaveResult.Failure(SchemaError.NotFound(schemaId.ToString()));
        }

        var deleteResult = await store.DeleteAsync(AttributeSchemaDso.EntityType, existing.Id, [], ct);

        return deleteResult switch
        {
            DeleteResult.Success => SchemaSaveResult.Success(schemaId, 0),
            DeleteResult.ConcurrencyConflict => SchemaSaveResult.Failure(SchemaError.VersionConflict()),
            _ => SchemaSaveResult.Failure(SchemaError.NotFound(schemaId.ToString()))
        };
    }

    /// <inheritdoc/>
    public async Task<SchemaQueryResult> QueryAsync(CancellationToken ct)
    {
        var store = await _storeFactory.GetStore(ct);
        var result = await store.QueryAsync<AttributeSchemaDso.V1>(
            AttributeSchemaDso.EntityType,
            filter: Query.All(),
            sort: SortParameter.Empty,
            DataRange.FromPage(1, DataRangeSize.MaxValue),
            ct);

        var summaries = result.Items.Select(e =>
        {
            var dso = e.Value;
            return new SchemaSummary
            {
                // TODO: SchemaId is not stored in AttributeSchemaDso.V1 (shared with user-management).
                // A coordinated V2 DSO or search field projection is needed to return real IDs here.
                SchemaId = SchemaId.Create("unknown"),
                DisplayName = null,
                AttributeCount = dso.AttributeDefinitions.Count,
                GroupCount = dso.Groups.Count
            };
        }).ToList();

        return SchemaQueryResult.Ok(summaries, summaries.Count);
    }

    // === Mapping ===

    private static AttributeSchemaDso.V1 ToDso(SchemaConfiguration schema) =>
        new([.. schema.AttributeDefinitions.Select(ToDso)],
            [.. schema.Groups.Select(ToDso)]);

    private static AttributeGroupDso.V1 ToDso(AttributeGroup group) =>
        new(group.Code.Value, group.DisplayName?.Value, group.Description?.Value, group.Order);

    private static AttributeDefinitionDso.V1 ToDso(AttributeDefinition def) =>
        new(def.Code.Value, ToTypeDso(def.AttributeType), def.Description?.Value, def.IsUnique,
            [.. def.Tags], def.GroupCode?.Value, def.Order, def.DisplayName?.Value, def.IsQueryable, def.IsRequired);

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

    private static SchemaConfiguration ToConfiguration(SchemaId schemaId, AttributeSchemaDso.V1 dso)
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

        return new SchemaConfiguration
        {
            SchemaId = schemaId,
            AttributeDefinitions = [.. attributes],
            Groups = [.. groups]
        };
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
