// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Public options type for SAML dynamic provider configuration. Enables customers
/// to use <c>ConfigureAuthenticationOptions&lt;SamlAuthenticationOptions, SamlProvider&gt;</c>
/// to customize SAML provider behavior per-scheme, matching the OIDC pattern with
/// <c>ConfigureAuthenticationOptions&lt;OpenIdConnectOptions, OidcProvider&gt;</c>.
/// </summary>
public class SamlAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The SP entity ID for this provider. When set, overrides the value from
    /// <see cref="Models.SamlProvider.SpEntityId"/> or the IdentityServer issuer default.
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    /// The outbound signing algorithm for requests to this provider.
    /// When set, overrides the value from <see cref="Models.SamlProvider.OutboundSigningAlgorithm"/>.
    /// </summary>
    public string? OutboundSigningAlgorithm { get; set; }

    /// <summary>
    /// Whether assertions from this provider must be signed.
    /// Null means use the value from <see cref="Models.SamlProvider"/>.
    /// </summary>
    public bool? WantAssertionsSigned { get; set; }

    /// <summary>
    /// Authentication scheme to sign in with after SAML authentication completes.
    /// When set, overrides the value from <see cref="Configuration.DynamicProviderOptions.SignInScheme"/>.
    /// </summary>
    public string? SignInScheme { get; set; }

    /// <summary>
    /// Authentication scheme to sign out with when a logout request is received.
    /// When set, overrides the value from <see cref="Configuration.DynamicProviderOptions.SignOutScheme"/>.
    /// </summary>
    public string? SignOutScheme { get; set; }
}
