// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Provides the persisted data storage object representation of an attribute schema.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal static class AttributeSchemaDso
{
    /// <summary>Gets the entity type for the attribute schema DSO.</summary>
    public static readonly EntityType EntityType = new(1501, "UserProfileSchemaDso");

    /// <summary>
    /// Version 1 of the attribute schema data storage object.
    /// </summary>
    /// <param name="AttributeDefinitions">The attribute definitions.</param>
    /// <param name="Groups">The attribute groups.</param>
    public sealed record V1(ICollection<AttributeDefinitionDso.V1> AttributeDefinitions, ICollection<AttributeGroupDso.V1> Groups) : IDataStorageObject
    {
        /// <summary>Gets the data storage object version descriptor.</summary>
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
