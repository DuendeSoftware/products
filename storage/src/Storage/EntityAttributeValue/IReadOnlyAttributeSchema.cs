// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Provides a read-only view of attribute definitions and groups in a schema.
/// </summary>
public interface IReadOnlyAttributeSchema
{
    /// <summary>
    ///     The attribute definitions in this schema, keyed by attribute code.
    /// </summary>
    IReadOnlyDictionary<AttributeCode, AttributeDefinition> AttributeDefinitions { get; }

    /// <summary>
    ///     The named groups defined in this schema, keyed by group name.
    /// </summary>
    IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups { get; }
}
