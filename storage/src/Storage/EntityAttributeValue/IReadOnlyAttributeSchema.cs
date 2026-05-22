// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public interface IReadOnlyAttributeSchema
{
    IReadOnlyDictionary<AttributeCode, AttributeDefinition> AttributeDefinitions { get; }

    /// <summary>
    ///     The named groups defined in this schema, keyed by group name.
    /// </summary>
    IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups { get; }
}
