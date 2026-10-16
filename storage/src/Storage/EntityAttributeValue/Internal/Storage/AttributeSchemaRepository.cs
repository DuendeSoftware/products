// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Persists attribute schemas as <see cref="AttributeSchemaDso.V1"/> under <see cref="DataCategoryName.DynamicSchemas"/>,
/// and maps them to and from <see cref="SchemaConfiguration"/>. Used by <see cref="StorageSchemaStore"/> and
/// <see cref="StorageSchemaAdmin"/>.
/// </summary>
internal sealed class AttributeSchemaRepository(
    IPartitionedStorageFactory partitionedStorageFactory,
    ILogger<AttributeSchemaRepository> logger)
{
    private static readonly DataCategoryName DataCategory = DataCategoryName.DynamicSchemas;

    internal async Task<CreateResult> CreateAsync(SchemaConfiguration schema, Ct ct)
    {
        var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategory, ct);
        return await storage.CreateAsync(
            UuidV7.New(),
            ToDso(schema),
            [DataStorageKey.Create(SchemaIdDskV1.Create(schema.SchemaId))],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration,
            [],
            ct);
    }

    /// <summary>Reads a schema by id, upconverting a schema saved by an earlier version.</summary>
    internal async Task<(SchemaConfiguration Schema, int Version)?> TryReadAsync(SchemaId schemaId, Ct ct)
    {
        var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategory, ct);
        var result = await ReadSchemaAsync(storage, schemaId, ct);
        return result.Found
            ? (ToConfiguration((AttributeSchemaDso.V1)result.Dso), result.Version.Value)
            : null;
    }

    internal async Task<UpdateResult> UpdateAsync(SchemaConfiguration schema, int expectedVersion, Ct ct)
    {
        var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategory, ct);
        var existing = await FindSchemaRowAsync(storage, schema.SchemaId, ct);
        if (!existing.Found)
        {
            return UpdateResult.DoesNotExist;
        }

        return await storage.UpdateAsync(
            existing.Id,
            ToDso(schema),
            expectedVersion,
            [DataStorageKey.Create(SchemaIdDskV1.Create(schema.SchemaId))],
            SearchFieldCollection.Empty,
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);
    }

    /// <summary>Deletes a schema by id. Returns <c>null</c> if there's no such schema.</summary>
    internal async Task<DeleteResult?> DeleteAsync(SchemaId schemaId, Ct ct)
    {
        var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategory, ct);
        var existing = await FindSchemaRowAsync(storage, schemaId, ct);
        return existing.Found
            ? await storage.DeleteAsync(AttributeSchemaDso.EntityType, existing.Id, [], ct)
            : null;
    }

    /// <summary>Returns a summary of every stored schema, reading all storage pages.</summary>
    internal async Task<IReadOnlyList<SchemaSummary>> QueryAsync(Ct ct)
    {
        var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategory, ct);
        var rows = new List<MetadataEnvelope<AttributeSchemaDso.V1>>();
        var page = 1;
        bool hasMoreData;
        do
        {
            var result = await storage.QueryAsync<AttributeSchemaDso.V1>(
                AttributeSchemaDso.EntityType,
                filter: Query.All(),
                sort: SortParameter.Empty,
                DataRange.FromPage(page++, DataRangeSize.MaxValue),
                ct);
            rows.AddRange(result.Items);
            hasMoreData = result.HasMoreData;
        }
        while (hasMoreData);

        return [.. ExcludeShadowedUserManagement1Row(rows)
            .Select(e => new SchemaSummary
            {
                SchemaId = ResolveQueriedSchemaId(e),
                DisplayName = e.Value.DisplayName,
                AttributeCount = e.Value.AttributeDefinitions.Count,
                GroupCount = e.Value.Groups.Count
            })];
    }
    // === Backwards compatibility ===
    //
    // Schemas saved before AttributeSchemaDso.V1 stored the schema id come in two shapes:
    //
    // 1. Duende.Storage pre-release builds of this class saved the schema with its SchemaIdDskV1 key,
    //    but without SchemaId in the document.
    // 2. User Management 1.x saved its profile schema ("user-profile") under a fixed row id in the
    //    default partition (pool 0), with no key and no SchemaId.
    //
    // FindSchemaRowAsync finds both shapes without writing; UpdateAsync and DeleteAsync use it, so their own
    // write upgrades or removes the row. ReadSchemaAsync (used by the GetAsync methods) also upconverts the row
    // in place on first read, so later reads take the normal key lookup. QueryAsync can't upconvert, because it
    // doesn't know the id of a pre-release row, so ResolveQueriedSchemaId reports those rows as "unknown" until
    // they're read by id once.
    //
    // Keep all backwards-compatibility handling in this section.

    /// <summary>The fixed row id User Management 1.x stored its profile schema under.</summary>
    private static readonly UuidV7 UserManagement1ProfileSchemaRowId =
        UuidV7.From(Guid.Parse("01968712-737f-70bf-a185-2da272266bd4"));

    /// <summary>The schema id of the User Management profile schema.</summary>
    private static readonly SchemaId UserManagement1ProfileSchemaId = SchemaId.Create("user-profile");

    /// <summary>The schema id <see cref="QueryAsync"/> reports for rows whose schema id isn't known yet.</summary>
    private static readonly SchemaId UnknownSchemaId = SchemaId.Create("unknown");

    /// <summary>
    /// Finds the stored row for a schema id: by key, or for "user-profile" the User Management 1.x row.
    /// Doesn't write. <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/> use this directly, so an update
    /// of an old row checks the caller's version and upgrades the row in the same write.
    /// </summary>
    private static async Task<StoreGetResult> FindSchemaRowAsync(IPartitionedStorage storage, SchemaId schemaId, Ct ct)
    {
        var result = await storage.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);

        // User Management 1.x stored the profile schema without a key. If a newer "user-profile" schema
        // exists, the key lookup above finds it and the old row is ignored.
        if (!result.Found && schemaId == UserManagement1ProfileSchemaId)
        {
            result = await storage.TryReadAsync(AttributeSchemaDso.EntityType, UserManagement1ProfileSchemaRowId, ct);
        }

        return result;
    }

    /// <summary>
    /// Reads a schema by id with <see cref="FindSchemaRowAsync"/>, and upconverts rows saved without a schema id.
    /// The returned DSO always has <see cref="AttributeSchemaDso.V1.SchemaId"/> set.
    /// </summary>
    private async Task<StoreGetResult> ReadSchemaAsync(IPartitionedStorage storage, SchemaId schemaId, Ct ct)
    {
        var result = await FindSchemaRowAsync(storage, schemaId, ct);

        if (!result.Found || result.Dso is not AttributeSchemaDso.V1 { SchemaId: null or "" } legacy)
        {
            return result;
        }

        var key = DataStorageKey.Create(SchemaIdDskV1.Create(schemaId));
        // The id was looked up, so it's known.
#pragma warning disable CA1308 // Normalize strings to uppercase
        var upconverted = legacy with { SchemaId = schemaId.Value.ToLowerInvariant() };
#pragma warning restore CA1308

        // Best effort: try once, and if the write doesn't succeed (for example a concurrent upconversion,
        // a transient database error or a read-only database user), return the upconverted value anyway.
        // The next read tries again.
        var version = result.Version.Value;
        try
        {
            var update = await storage.UpdateAsync(
                result.Id.Value,
                upconverted,
                result.Version.Value,
                [key],
                SearchFieldCollection.Empty,
                expiration: Expiration.NoExpiration,
                outboxEvents: [],
                ct);
            if (update == UpdateResult.Success)
            {
                version++;
            }
            else
            {
                logger.SchemaUpconversionNotSaved(upconverted.SchemaId, update.ToString());
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types: a failed upconversion write must not fail the read.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            logger.SchemaUpconversionFailed(ex, upconverted.SchemaId);
        }

        return StoreGetResult.IsFound(upconverted, result.Id.Value, version, result.CreatedAt, result.LastUpdatedAt);
    }

    /// <summary>
    /// Returns the schema id for a queried row: the stored id, "user-profile" for the User Management 1.x row,
    /// or "unknown" for a pre-release row that hasn't been read by id yet.
    /// </summary>
    private static SchemaId ResolveQueriedSchemaId(MetadataEnvelope<AttributeSchemaDso.V1> row) =>
        row.Value.SchemaId is { Length: > 0 } schemaId ? SchemaId.Load(schemaId)
        : row.Id == UserManagement1ProfileSchemaRowId.Value ? UserManagement1ProfileSchemaId
        : UnknownSchemaId;

    /// <summary>
    /// Leaves the User Management 1.x row out of query results when a newer "user-profile" schema exists,
    /// because <see cref="ReadSchemaAsync"/> ignores that row in that case too.
    /// </summary>
    private static IEnumerable<MetadataEnvelope<AttributeSchemaDso.V1>> ExcludeShadowedUserManagement1Row(
        IReadOnlyList<MetadataEnvelope<AttributeSchemaDso.V1>> rows)
    {
        var newerProfileExists = rows.Any(r =>
            r.Value.SchemaId is { Length: > 0 } schemaId && SchemaId.Load(schemaId) == UserManagement1ProfileSchemaId);

        return newerProfileExists
            ? rows.Where(r => r.Id != UserManagement1ProfileSchemaRowId.Value || r.Value.SchemaId is { Length: > 0 })
            : rows;
    }

    // === Mapping ===

    private static AttributeSchemaDso.V1 ToDso(SchemaConfiguration schema) =>
        new(schema.SchemaId.Value,
            schema.DisplayName,
            schema.Description,
            [.. schema.AttributeDefinitions.Select(ToDso)],
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

    private static SchemaConfiguration ToConfiguration(AttributeSchemaDso.V1 dso) =>
        new()
        {
            // ReadSchemaAsync always returns a DSO with the schema id set.
            SchemaId = SchemaId.Load(dso.SchemaId ?? throw new InvalidOperationException("The schema DSO has no schema id.")),
            DisplayName = dso.DisplayName,
            Description = dso.Description,
            AttributeDefinitions = ToAttributeDefinitions(dso.AttributeDefinitions),
            Groups = ToGroups(dso.Groups)
        };

    private static AttributeDefinition[] ToAttributeDefinitions(ICollection<AttributeDefinitionDso.V1> definitions) =>
        [.. definitions.Select(att => AttributeDefinition.Load(
            AttributeCode.Load(att.Code),
            ToTypeValueObject(att.Type),
            att.Description is not null ? AttributeDescription.Load(att.Description) : null,
            att.DisplayName is not null ? AttributeDisplayName.Load(att.DisplayName) : null,
            att.IsUnique,
            att.IsQueryable,
            att.IsRequired,
            att.Tags,
            att.GroupCode is not null ? AttributeGroupCode.Load(att.GroupCode) : null,
            att.Order))];

    private static AttributeGroup[] ToGroups(ICollection<AttributeGroupDso.V1> groups) =>
        [.. groups.Select(g => new AttributeGroup(
            AttributeGroupCode.Load(g.Code),
            g.DisplayName is not null ? AttributeDisplayName.Load(g.DisplayName) : null,
            g.Description is not null ? AttributeDescription.Load(g.Description) : null,
            g.Order))];

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
