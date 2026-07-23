// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     A lightweight summary of a schema, used in query results.
/// </summary>
public sealed class SchemaSummary
{
    /// <summary>
    ///     The schema identifier.
    /// </summary>
    public required SchemaId SchemaId { get; set; }

    /// <summary>
    ///     An optional human-readable display name for the schema.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     The number of attribute definitions in the schema.
    /// </summary>
    public int AttributeCount { get; set; }

    /// <summary>
    ///     The number of attribute groups in the schema.
    /// </summary>
    public int GroupCount { get; set; }
}
