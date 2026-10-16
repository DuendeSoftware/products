// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal.Storage;

namespace Duende.Storage.EntityAttributeValue.Internal;

/// <summary>
///     A storage-backed <see cref="ISchemaStore"/> that reads schemas from the database.
/// </summary>
internal sealed class StorageSchemaStore(AttributeSchemaRepository repository) : ISchemaStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyAttributeSchema> GetAsync(SchemaId schemaId, CancellationToken ct) =>
        await repository.TryReadAsync(schemaId, ct) is { } stored
            ? SchemaConfigurationMapper.ToReadOnlySchema(stored.Schema)
            : AttributeSchema.Empty;
}
