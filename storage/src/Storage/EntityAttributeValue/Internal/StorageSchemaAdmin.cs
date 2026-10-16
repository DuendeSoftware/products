// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;

namespace Duende.Storage.EntityAttributeValue.Internal;

/// <summary>
///     A storage-backed <see cref="ISchemaAdmin"/> that creates, reads, updates, deletes and lists
///     schemas in the database.
/// </summary>
internal sealed class StorageSchemaAdmin(AttributeSchemaRepository repository) : ISchemaAdmin
{
    /// <inheritdoc/>
    public async Task<SaveResult<SchemaId>> CreateAsync(SchemaConfiguration schema, CancellationToken ct) =>
        await repository.CreateAsync(schema, ct) switch
        {
            CreateResult.Success => SaveResult.Success(schema.SchemaId, (DataVersion)1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                SaveResult.Failure<SchemaId>(StorageError.AlreadyExists("schema", schema.SchemaId.ToString())),
            CreateResult.ConcurrencyConflict => SaveResult.Failure<SchemaId>(StorageError.VersionConflict()),
            _ => SaveResult.Failure<SchemaId>(StorageError.ValidationFailed("Failed to create schema."))
        };

    /// <inheritdoc/>
    public async Task<GetResult<SchemaConfiguration>> GetAsync(SchemaId schemaId, CancellationToken ct) =>
        await repository.TryReadAsync(schemaId, ct) is { } stored
            ? GetResult.Found(stored.Schema, (DataVersion)stored.Version)
            : GetResult.NotFound<SchemaConfiguration>();

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

        return await repository.UpdateAsync(schema, expectedVersion.Value, ct) switch
        {
            UpdateResult.Success => SaveResult.Success(schema.SchemaId, (DataVersion)(expectedVersion.Value + 1)),
            UpdateResult.UnexpectedVersion => SaveResult.Failure<SchemaId>(StorageError.VersionConflict()),
            UpdateResult.KeyConflict => SaveResult.Failure<SchemaId>(StorageError.AlreadyExists("schema", schema.SchemaId.ToString())),
            _ => SaveResult.Failure<SchemaId>(StorageError.NotFound("schema", schemaId.ToString()))
        };
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SchemaId>> DeleteAsync(SchemaId schemaId, CancellationToken ct) =>
        await repository.DeleteAsync(schemaId, ct) switch
        {
            DeleteResult.Success => SaveResult.Success(schemaId, (DataVersion)0),
            DeleteResult.ConcurrencyConflict => SaveResult.Failure<SchemaId>(StorageError.VersionConflict()),
            _ => SaveResult.Failure<SchemaId>(StorageError.NotFound("schema", schemaId.ToString()))
        };

    /// <inheritdoc/>
    public async Task<QueryResult<SchemaSummary>> QueryAsync(CancellationToken ct)
    {
        var summaries = await repository.QueryAsync(ct);
        return new QueryResult<SchemaSummary>
        {
            Items = summaries,
            TotalCount = summaries.Count,
            HasMoreData = false
        };
    }
}
