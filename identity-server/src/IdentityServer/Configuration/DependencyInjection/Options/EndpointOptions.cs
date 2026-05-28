// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Controls which protocol endpoints are enabled or disabled in IdentityServer.
/// </summary>
public class EndpointsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the authorize endpoint (<c>/connect/authorize</c>) is enabled, which is the entry point for
    /// interactive authorization code and implicit flows.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableAuthorizeEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether support for the <c>request_uri</c> parameter on the authorize endpoint is enabled, allowing
    /// JWT-Secured Authorization Requests (JAR) to be passed by reference.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c> due to the security implications described in
    /// <see href="https://datatracker.ietf.org/doc/rfc9101/">RFC 9101 section 10.4</see>.
    /// Enable only when clients require JAR by reference and the associated risks are mitigated.
    /// </remarks>
    public bool EnableJwtRequestUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the token endpoint (<c>/connect/token</c>) is enabled, which issues access tokens, refresh
    /// tokens, and identity tokens.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableTokenEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the user info endpoint (<c>/connect/userinfo</c>) is enabled, which returns claims about
    /// the authenticated user.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableUserInfoEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the discovery endpoint (<c>/.well-known/openid-configuration</c>) is enabled, which
    /// publishes server metadata.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableDiscoveryEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the end-session endpoint (<c>/connect/endsession</c>) is enabled, which initiates user
    /// logout.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableEndSessionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the check-session endpoint (<c>/connect/checksession</c>) is enabled, which supports
    /// OpenID Connect session management via an iframe.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableCheckSessionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the token revocation endpoint (<c>/connect/revocation</c>) is enabled, which allows clients
    /// to revoke access or refresh tokens.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableTokenRevocationEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the introspection endpoint (<c>/connect/introspect</c>) is enabled, which allows resource
    /// servers to validate tokens.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableIntrospectionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the device authorization endpoint (<c>/connect/deviceauthorization</c>) is enabled, which
    /// supports the OAuth 2.0 Device Authorization Grant.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableDeviceAuthorizationEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the backchannel authentication endpoint (<c>/connect/ciba</c>) is enabled, which supports
    /// Client-Initiated Backchannel Authentication (CIBA).
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableBackchannelAuthenticationEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the pushed authorization endpoint (<c>/connect/par</c>) is enabled, which allows clients
    /// to push authorization request parameters before initiating the authorization flow.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnablePushedAuthorizationEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the OAuth 2.0 authorization server metadata endpoint
    /// (<c>/.well-known/oauth-authorization-server</c>) is enabled.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableOAuth2MetadataEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the SAML 2.0 metadata endpoint is enabled.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>.</remarks>
    public bool EnableSamlMetadataEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SAML 2.0 single sign-on (SSO) endpoint is enabled.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>.</remarks>
    public bool EnableSamlSigninEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SAML 2.0 sign-in callback endpoint is enabled, which handles the service provider's
    /// response after authentication.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>.</remarks>
    public bool EnableSamlSigninCallbackEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SAML 2.0 Single Logout (SLO) endpoint is enabled.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>.</remarks>
    public bool EnableSamlLogoutEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SAML 2.0 Single Logout callback endpoint is enabled, which handles the service
    /// provider's response after logout.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>.</remarks>
    public bool EnableSamlLogoutCallbackEndpoint { get; set; }
}
