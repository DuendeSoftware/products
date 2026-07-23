// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
    ///     A <see cref="SchemaSaveResult"/> indicating success (with version) or failure
    ///     (e.g., a schema with the same ID already exists).
    /// </returns>
    Task<SchemaSaveResult> CreateAsync(SchemaConfiguration schema, CancellationToken ct);

    /// <summary>
    ///     Gets a schema by its identifier.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     A <see cref="SchemaGetResult"/> with the configuration if found, or a not-found result.
    /// </returns>
    Task<SchemaGetResult> GetAsync(SchemaId schemaId, CancellationToken ct);

    /// <summary>
    ///     Updates an existing schema using optimistic concurrency.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="schema">The updated schema configuration.</param>
    /// <param name="expectedVersion">The version of the schema when last retrieved.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     A <see cref="SchemaSaveResult"/> indicating success or failure
    ///     (e.g., version conflict or not found).
    /// </returns>
    Task<SchemaSaveResult> UpdateAsync(SchemaId schemaId, SchemaConfiguration schema, int expectedVersion, CancellationToken ct);

    /// <summary>
    ///     Deletes a schema by its identifier.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     A <see cref="SchemaSaveResult"/> indicating success or failure (e.g., not found).
    /// </returns>
    Task<SchemaSaveResult> DeleteAsync(SchemaId schemaId, CancellationToken ct);

    /// <summary>
    ///     Queries all schemas.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="SchemaQueryResult"/> with schema summaries.</returns>
    Task<SchemaQueryResult> QueryAsync(CancellationToken ct);
}
