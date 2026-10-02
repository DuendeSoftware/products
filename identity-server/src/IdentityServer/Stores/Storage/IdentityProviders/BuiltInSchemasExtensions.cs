// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

/// <summary>
/// IdentityServer's built-in schemas.
/// </summary>
public static class BuiltInSchemasExtensions
{
    extension(BuiltInSchemas)
    {
        /// <summary>
        /// The built-in schema for OIDC identity providers, registered by
        /// <c>AddOidcDynamicProvider()</c>. Each access returns a new, mutable instance.
        /// </summary>
        public static SchemaConfiguration OidcProvider => DefaultOidcProviderSchema.CreateSchema();

        /// <summary>
        /// The built-in schema for SAML identity providers, registered by
        /// <c>AddSamlDynamicProvider()</c>. Each access returns a new, mutable instance.
        /// </summary>
        public static SchemaConfiguration SamlProvider => DefaultSamlProviderSchema.CreateSchema();
    }
}
