// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Provides read-only access to attribute schemas for runtime validation purposes.
/// </summary>
public interface ISchemaStore
{
    /// <summary>
    ///     Gets a schema by its identifier.
    /// </summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    ///     The schema if found; <c>null</c> if no schema with the given ID exists.
    /// </returns>
    Task<IReadOnlyAttributeSchema?> GetAsync(SchemaId schemaId, CancellationToken ct);
}
