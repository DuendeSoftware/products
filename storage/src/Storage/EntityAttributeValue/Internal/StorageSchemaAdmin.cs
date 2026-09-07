// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;

namespace Duende.Storage.EntityAttributeValue.Internal;

/// <summary>
///     A storage-backed implementation of <see cref="ISchemaStore"/> and <see cref="ISchemaAdmin"/>
///     that persists schemas to the database via the storage layer.
/// </summary>
internal sealed class StorageSchemaAdmin : ISchemaStore, ISchemaAdmin
{
    private readonly IStorageFactory _storageFactory;

    /// <summary>
    ///     Initialises a new <see cref="StorageSchemaAdmin"/>.
    /// </summary>
    /// <param name="storageFactory">The storage factory for obtaining a scoped storage.</param>
    public StorageSchemaAdmin(IStorageFactory storageFactory) =>
        _storageFactory = storageFactory;

    /// <inheritdoc/>
    public async Task<IReadOnlyAttributeSchema> GetAsync(SchemaId schemaId, CancellationToken ct)
    {
        var storage = await _storageFactory.GetStorage(ct);
        var result = await storage.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!result.Found)
        {
            return AttributeSchema.Empty;
        }

        var dso = (AttributeSchemaDso.V1)result.Dso;
        var config = ToConfiguration(schemaId, dso);
        return SchemaConfigurationMapper.ToReadOnlySchema(config);
    }

    /// <inheritdoc/>
    async Task<SaveResult<SchemaId>> ISchemaAdmin.CreateAsync(SchemaConfiguration schema, CancellationToken ct)
    {
        var storage = await _storageFactory.GetStorage(ct);
        var id = UuidV7.New();
        var createResult = await storage.CreateAsync(
            id,
            ToDso(schema),
            [DataStorageKey.Create(SchemaIdDskV1.Create(schema.SchemaId))],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration,
            [],
            ct);

        return createResult switch
        {
            CreateResult.Success => SaveResult.Success(schema.SchemaId, (DataVersion)1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                SaveResult.Failure<SchemaId>(StorageError.AlreadyExists("schema", schema.SchemaId.ToString())),
            CreateResult.ConcurrencyConflict => SaveResult.Failure<SchemaId>(StorageError.VersionConflict()),
            _ => SaveResult.Failure<SchemaId>(StorageError.ValidationFailed("Failed to create schema."))
        };
    }

    /// <inheritdoc/>
    async Task<GetResult<SchemaConfiguration>> ISchemaAdmin.GetAsync(SchemaId schemaId, CancellationToken ct)
    {
        var storage = await _storageFactory.GetStorage(ct);
        var result = await storage.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!result.Found)
        {
            return GetResult.NotFound<SchemaConfiguration>();
        }

        var config = ToConfiguration(schemaId, (AttributeSchemaDso.V1)result.Dso);
        return GetResult.Found(config, (DataVersion)result.Version.Value);
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SchemaId>> UpdateAsync(
        SchemaId schemaId,
        SchemaConfiguration schema,
        DataVersion expectedVersion,
        CancellationToken ct)
    {
        if (schemaId != schema.SchemaId)
        {
            return SaveResult.Failure<SchemaId>(
                StorageError.ValidationFailed("Schema ID in the body must match the route schema ID."));
        }

        var storage = await _storageFactory.GetStorage(ct);
        var existing = await storage.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!existing.Found)
        {
            return SaveResult.Failure<SchemaId>(StorageError.NotFound("schema", schemaId.ToString()));
        }

        var updateResult = await storage.UpdateAsync(
            existing.Id,
            ToDso(schema),
            expectedVersion.Value,
            [DataStorageKey.Create(SchemaIdDskV1.Create(schema.SchemaId))],
            SearchFieldCollection.Empty,
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

        return updateResult switch
        {
            UpdateResult.Success => SaveResult.Success(schema.SchemaId, (DataVersion)(expectedVersion.Value + 1)),
            UpdateResult.UnexpectedVersion => SaveResult.Failure<SchemaId>(StorageError.VersionConflict()),
            UpdateResult.KeyConflict => SaveResult.Failure<SchemaId>(StorageError.AlreadyExists("schema", schema.SchemaId.ToString())),
            _ => SaveResult.Failure<SchemaId>(StorageError.NotFound("schema", schemaId.ToString()))
        };
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SchemaId>> DeleteAsync(SchemaId schemaId, CancellationToken ct)
    {
        var storage = await _storageFactory.GetStorage(ct);

        var existing = await storage.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        if (!existing.Found)
        {
            return SaveResult.Failure<SchemaId>(StorageError.NotFound("schema", schemaId.ToString()));
        }

        var deleteResult = await storage.DeleteAsync(AttributeSchemaDso.EntityType, existing.Id, [], ct);

        return deleteResult switch
        {
            DeleteResult.Success => SaveResult.Success(schemaId, (DataVersion)0),
            DeleteResult.ConcurrencyConflict => SaveResult.Failure<SchemaId>(StorageError.VersionConflict()),
            _ => SaveResult.Failure<SchemaId>(StorageError.NotFound("schema", schemaId.ToString()))
        };
    }

    /// <inheritdoc/>
    public async Task<QueryResult<SchemaSummary>> QueryAsync(CancellationToken ct)
    {
        var storage = await _storageFactory.GetStorage(ct);
        var result = await storage.QueryAsync<AttributeSchemaDso.V1>(
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

        return new QueryResult<SchemaSummary>
        {
            Items = summaries,
            TotalCount = summaries.Count,
            HasMoreData = false
        };
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
