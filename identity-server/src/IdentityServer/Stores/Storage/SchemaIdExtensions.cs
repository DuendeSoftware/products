// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
///     Provides well-known <see cref="SchemaId"/> constants for built-in IdentityServer
///     configuration stores.
/// </summary>
public static class SchemaIdExtensions
{
    private static readonly SchemaId ClientSchemaId = SchemaId.Create("client");
    private static readonly SchemaId ApiResourceSchemaId = SchemaId.Create("api-resource");
    private static readonly SchemaId ApiScopeSchemaId = SchemaId.Create("api-scope");
    private static readonly SchemaId IdentityResourceSchemaId = SchemaId.Create("identity-resource");

    extension(SchemaId)
    {
        /// <summary>The well-known schema ID for client extended properties.</summary>
        public static SchemaId Client => ClientSchemaId;

        /// <summary>The well-known schema ID for API resource extended properties.</summary>
        public static SchemaId ApiResource => ApiResourceSchemaId;

        /// <summary>The well-known schema ID for API scope extended properties.</summary>
        public static SchemaId ApiScope => ApiScopeSchemaId;

        /// <summary>The well-known schema ID for identity resource extended properties.</summary>
        public static SchemaId IdentityResource => IdentityResourceSchemaId;
    }
}
