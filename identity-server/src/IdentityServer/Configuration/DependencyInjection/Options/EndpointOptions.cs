// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Configures which endpoints are enabled or disabled.
/// </summary>
public class EndpointsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the authorize endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the authorize endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableAuthorizeEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets if JWT request_uri processing is enabled on the authorize endpoint.
    /// </summary>
    public bool EnableJwtRequestUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the token endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the token endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableTokenEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the user info endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the user info endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableUserInfoEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the discovery document endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the disdovery document endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableDiscoveryEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the end session endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the end session endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableEndSessionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the check session endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the check session endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableCheckSessionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the token revocation endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the token revocation endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableTokenRevocationEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the introspection endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the introspection endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableIntrospectionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the device authorization endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the device authorization endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableDeviceAuthorizationEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the backchannel authentication endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the backchannel authentication endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableBackchannelAuthenticationEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the pushed authorization endpoint is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the pushed authorization endpoint is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnablePushedAuthorizationEndpoint { get; set; } = true;
}
