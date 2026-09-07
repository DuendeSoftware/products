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
    private static readonly SchemaId SamlServiceProviderSchemaId = SchemaId.Create("saml-service-provider");

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

        /// <summary>The well-known schema ID for SAML service provider extended properties.</summary>
        public static SchemaId SamlServiceProvider => SamlServiceProviderSchemaId;

        /// <summary>
        /// Creates a per-type schema ID for identity provider extended properties.
        /// The schema ID is derived from the provider's <c>Type</c> field (e.g. <c>"oidc"</c> → <c>"idp:oidc"</c>).
        /// </summary>
        public static SchemaId IdentityProvider(string type) => SchemaId.Create($"idp:{type}");
    }
}
