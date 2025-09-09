// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Configuration;

public enum RegistrationEndpointMode
{
    /// <summary>
    /// Will not show a registration endpoint in the discovery document
    /// </summary>
    None,

    /// <summary>
    /// Will use the static URL from <see cref="DynamicClientRegistrationDiscoveryOptions.StaticRegistrationEndpoint"/>
    /// </summary>
    Static,

    /// <summary>
    /// Will infer the URL dynamically based on the host
    /// </summary>
    Inferred
}

public class DynamicClientRegistrationDiscoveryOptions
{
    /// <summary>
    /// Gets or sets the type of the registration endpoint
    /// </summary>
    /// <value>
    /// The type of the registration endpoint.
    /// </value>
    public RegistrationEndpointMode RegistrationEndpointMode { get; set; } = RegistrationEndpointMode.None;

    /// <summary>
    /// Gets or sets the custom registration endpoint
    /// </summary>
    /// <value>
    /// The URL of the authorization endpoint to use in the discovery document if <see cref="RegistrationEndpointMode"/> is set to <see cref="RegistrationEndpointMode.Static"/>.
    /// </value>
    public Uri? StaticRegistrationEndpoint { get; set; }
}
