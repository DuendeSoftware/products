// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     A mutable configuration object for schema CRUD operations.
///     Callers can <c>Get</c>, modify attribute definitions and groups, then pass to
///     <see cref="ISchemaAdmin.UpdateAsync"/> or <see cref="ISchemaAdmin.CreateAsync"/>.
/// </summary>
public sealed class SchemaConfiguration
{
    /// <summary>
    ///     The schema identifier. Used as the discriminated storage key.
    /// </summary>
    public required SchemaId SchemaId { get; set; }

    /// <summary>
    ///     An optional human-readable display name for the schema.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     An optional description of the schema's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The attribute definitions in this schema.
    /// </summary>
    public ICollection<AttributeDefinition> AttributeDefinitions { get; init; } = [];

    /// <summary>
    ///     The attribute groups in this schema.
    /// </summary>
    public ICollection<AttributeGroup> Groups { get; init; } = [];

    /// <summary>
    ///     The data version for optimistic concurrency. <c>null</c> for new schemas.
    /// </summary>
    public int? Version { get; set; }
}
