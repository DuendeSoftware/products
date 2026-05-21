// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

public static class AttributeSchemaDso
{
    public static readonly EntityType EntityType = new(1501, "UserProfileSchemaDso");

    public sealed record V1(ICollection<AttributeDefinitionDso.V1> AttributeDefinitions, ICollection<AttributeGroupDso.V1> Groups) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
