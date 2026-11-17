// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

using Duende.IdentityServer.DataExtensions.Internal.Storage;

namespace Duende.IdentityServer.DataExtensions.Internal;

/// <summary>
///     A storage-backed <see cref="ISchemaStore"/> that reads schemas from the database.
/// </summary>
internal sealed class StorageSchemaStore(AttributeSchemaRepository repository) : ISchemaStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyAttributeSchema> GetAsync(SchemaId schemaId, Ct ct) =>
        await repository.TryReadAsync(schemaId, ct) is { } stored
            ? SchemaConfigurationMapper.ToReadOnlySchema(stored.Schema)
            : AttributeSchema.Empty;
}
