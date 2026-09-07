// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

/// <summary>
/// Provides the collection of built-in schemas that are automatically available in every
/// IdentityServer deployment, regardless of whether the caller registers custom schemas.
/// </summary>
internal static class BuiltInSchemas
{
    /// <summary>
    /// All built-in schemas. Always merged into any schema store to ensure standard
    /// identity provider types (e.g. OIDC, SAML) work without explicit user registration.
    /// </summary>
    internal static readonly IReadOnlyList<SchemaConfiguration> All =
    [
        DefaultOidcProviderSchema.Schema,
        DefaultSamlProviderSchema.Schema
    ];
}
