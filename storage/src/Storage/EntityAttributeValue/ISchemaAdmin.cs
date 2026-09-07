// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Provides administrative CRUD operations for attribute schemas.
/// </summary>
public interface ISchemaAdmin
{
    /// <summary>
    ///     Creates a new schema.
    /// </summary>
    /// <param name="schema">The schema configuration to create.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     A <see cref="SaveResult{TId}"/> indicating success (with version) or failure
    ///     (e.g., a schema with the same ID already exists).
    /// </returns>
    Task<SaveResult<SchemaId>> CreateAsync(SchemaConfiguration schema, CancellationToken ct);

    /// <summary>
    ///     Gets a schema by its identifier.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     A <see cref="GetResult{T}"/> with the configuration if found, or a not-found result.
    /// </returns>
    Task<GetResult<SchemaConfiguration>> GetAsync(SchemaId schemaId, CancellationToken ct);

    /// <summary>
    ///     Updates an existing schema using optimistic concurrency.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="schema">The updated schema configuration.</param>
    /// <param name="expectedVersion">The version of the schema when last retrieved.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     A <see cref="SaveResult{TId}"/> indicating success or failure
    ///     (e.g., version conflict or not found).
    /// </returns>
    Task<SaveResult<SchemaId>> UpdateAsync(SchemaId schemaId, SchemaConfiguration schema, DataVersion expectedVersion, CancellationToken ct);

    /// <summary>
    ///     Deletes a schema by its identifier.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     A <see cref="SaveResult{TId}"/> indicating success or failure (e.g., not found).
    /// </returns>
    Task<SaveResult<SchemaId>> DeleteAsync(SchemaId schemaId, CancellationToken ct);

    /// <summary>
    ///     Queries all schemas.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="QueryResult{TItem}"/> with schema summaries.</returns>
    Task<QueryResult<SchemaSummary>> QueryAsync(CancellationToken ct);
}
