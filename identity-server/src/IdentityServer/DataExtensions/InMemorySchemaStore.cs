// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

using Duende.IdentityServer.DataExtensions.Internal;

namespace Duende.IdentityServer.DataExtensions;

/// <summary>
///     An in-memory implementation of <see cref="ISchemaStore"/> that resolves schemas from
///     a fixed set of <see cref="SchemaConfiguration"/> objects provided at construction time.
///     Use this for deployments that define schemas in code or configuration.
/// </summary>
public sealed class InMemorySchemaStore : ISchemaStore
{
    private readonly Dictionary<SchemaId, IReadOnlyAttributeSchema> _schemas;

    /// <summary>
    ///     Initialises a new <see cref="InMemorySchemaStore"/> with the given schema configurations.
    /// </summary>
    /// <param name="schemas">The schemas to make available for resolution.</param>
    public InMemorySchemaStore(IEnumerable<SchemaConfiguration> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        _schemas = new Dictionary<SchemaId, IReadOnlyAttributeSchema>();
        foreach (var schema in schemas)
        {
            _schemas[schema.SchemaId] = SchemaConfigurationMapper.ToReadOnlySchema(schema);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyAttributeSchema> GetAsync(SchemaId schemaId, Ct ct) =>
        Task.FromResult(_schemas.TryGetValue(schemaId, out var schema)
            ? schema
            : (IReadOnlyAttributeSchema)AttributeSchema.Empty);
}
