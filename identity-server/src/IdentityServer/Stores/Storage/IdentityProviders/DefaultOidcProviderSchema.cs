// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

/// <summary>
/// Built-in attribute definitions for OIDC identity providers.
/// These correspond to the properties used by <see cref="Models.OidcProvider"/> and are
/// automatically registered in the schema store so that OIDC providers work out-of-the-box.
/// </summary>
internal static class DefaultOidcProviderSchema
{
    /// <summary>The base address of the OIDC provider (e.g. <c>https://idp.example.com</c>).</summary>
    public static readonly TypedAttributeDefinition<string> Authority =
        new(AttributeCode.Create("Authority"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>The client ID used to authenticate with the external OIDC provider.</summary>
    public static readonly TypedAttributeDefinition<string> ClientId =
        new(AttributeCode.Create("ClientId"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>The client secret used to authenticate with the external OIDC provider.</summary>
    public static readonly TypedAttributeDefinition<string> ClientSecret =
        new(AttributeCode.Create("ClientSecret"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>The response type (e.g. <c>id_token</c>). Defaults to <c>id_token</c> if not set.</summary>
    public static readonly TypedAttributeDefinition<string> ResponseType =
        new(AttributeCode.Create("ResponseType"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>Space-separated scope values (e.g. <c>openid profile</c>). Defaults to <c>openid</c> if not set.</summary>
    public static readonly TypedAttributeDefinition<string> Scope =
        new(AttributeCode.Create("Scope"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>
    /// Whether to contact the userinfo endpoint. Stored as <c>"true"</c> or <c>"false"</c>.
    /// Defaults to <c>true</c> if not set.
    /// </summary>
    /// <remarks>
    /// Defined as <c>string</c> (not <c>bool</c>) because <see cref="Models.OidcProvider"/> reads these
    /// values from the Properties dictionary via string comparison, and
    /// <see cref="EavPropertyMapper.ExtractStringProperties"/> must round-trip them as strings.
    /// </remarks>
    public static readonly TypedAttributeDefinition<string> GetClaimsFromUserInfoEndpoint =
        new(AttributeCode.Create("GetClaimsFromUserInfoEndpoint"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>
    /// Whether PKCE should be used. Stored as <c>"true"</c> or <c>"false"</c>.
    /// Defaults to <c>true</c> if not set.
    /// </summary>
    /// <remarks>
    /// Defined as <c>string</c> (not <c>bool</c>) because <see cref="Models.OidcProvider"/> reads these
    /// values from the Properties dictionary via string comparison, and
    /// <see cref="EavPropertyMapper.ExtractStringProperties"/> must round-trip them as strings.
    /// </remarks>
    public static readonly TypedAttributeDefinition<string> UsePkce =
        new(AttributeCode.Create("UsePkce"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>
    /// The built-in schema for OIDC identity providers, registered with schema ID <c>idp:oidc</c>.
    /// </summary>
    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.IdentityProvider("oidc"),
        DisplayName = "OIDC Identity Provider",
        Description = "Built-in schema for OpenID Connect identity providers.",
        AttributeDefinitions =
        [
            Authority,
            ClientId,
            ClientSecret,
            ResponseType,
            Scope,
            GetClaimsFromUserInfoEndpoint,
            UsePkce
        ]
    };
}
