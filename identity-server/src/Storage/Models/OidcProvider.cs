// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models an OIDC identity provider
/// </summary>
public record OidcProvider : IdentityProvider
{
    /// <summary>
    /// Ctor
    /// </summary>
    public OidcProvider() : base("oidc")
    {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    public OidcProvider(IdentityProvider other) : base("oidc", other)
    {
    }

    /// <summary>
    /// Gets or sets the base address of the OIDC provider.
    /// </summary>
    public string? Authority
    {
        get => this["Authority"];
        set => this["Authority"] = value;
    }
    /// <summary>
    /// Gets or sets the response type. Defaults to "id_token".
    /// </summary>
    public string ResponseType
    {
        get => this["ResponseType"] ?? "id_token";
        set => this["ResponseType"] = value;
    }
    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string? ClientId
    {
        get => this["ClientId"];
        set => this["ClientId"] = value;
    }
    /// <summary>
    /// Gets or sets the client secret used to authenticate with the external OIDC provider.
    /// By default this is the plaintext client secret — great consideration should be taken if this value is to be stored
    /// as plaintext in the store. It is possible to store this in a protected way and then unprotect when loading from
    /// the store by implementing a custom <c>IIdentityProviderStore</c> or registering a custom
    /// <c>IConfigureNamedOptions&lt;OpenIdConnectOptions&gt;</c>.
    /// </summary>
    public string? ClientSecret
    {
        get => this["ClientSecret"];
        set => this["ClientSecret"] = value;
    }
    /// <summary>
    /// Gets or sets the space-separated list of scope values to request from the external OIDC provider. Defaults to <c>openid</c>.
    /// </summary>
    public string Scope
    {
        get => this["Scope"] ?? "openid";
        set => this["Scope"] = value;
    }
    /// <summary>
    /// Gets or sets a value indicating whether the userinfo endpoint is to be contacted. Defaults to true.
    /// </summary>
    public bool GetClaimsFromUserInfoEndpoint
    {
        get => this["GetClaimsFromUserInfoEndpoint"] == null || "true".Equals(this["GetClaimsFromUserInfoEndpoint"], StringComparison.Ordinal);
        set => this["GetClaimsFromUserInfoEndpoint"] = value ? "true" : "false";
    }
    /// <summary>
    /// Gets or sets a value indicating whether PKCE should be used. Defaults to true.
    /// </summary>
    public bool UsePkce
    {
        get => this["UsePkce"] == null || "true".Equals(this["UsePkce"], StringComparison.Ordinal);
        set => this["UsePkce"] = value ? "true" : "false";
    }

    /// <summary>
    /// Gets the collection of individual scope values parsed from the <see cref="Scope"/> string.
    /// </summary>
    public IEnumerable<string> Scopes
    {
        get
        {
            var scopes = Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            return scopes;
        }
    }
}
