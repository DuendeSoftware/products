// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.Spaces;

/// <summary>
///     Provides well-known <see cref="SchemaId"/> constants for Spaces entities.
/// </summary>
public static class SchemaIdExtensions
{
    private static readonly SchemaId SpaceSchemaId = SchemaId.Create("space");

    extension(SchemaId)
    {
        /// <summary>Gets the schema ID for space extended properties.</summary>
        public static SchemaId Space => SpaceSchemaId;
    }
}
