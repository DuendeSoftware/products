// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Configuration;

public enum RegistrationEndpointType
{
    /// <summary>
    /// Will not show a registration endpoint in the discovery document
    /// </summary>
    None,

    /// <summary>
    /// Will use the static URL from <see cref="DynamicClientRegistrationDiscoveryOptions.CustomRegistrationEndpoint"/>
    /// </summary>
    Static,

    /// <summary>
    /// Will generate the URL dynamically based on the host
    /// </summary>
    Dynamic
}

public class DynamicClientRegistrationDiscoveryOptions
{
    /// <summary>
    /// Gets or sets the type of the registration endpoint
    /// </summary>
    /// <value>
    /// The type of the registration endpoint.
    /// </value>
    public RegistrationEndpointType RegistrationEndpointType { get; set; } = RegistrationEndpointType.None;

    /// <summary>
    /// Gets or sets the custom registration endpoint
    /// </summary>
    /// <value>
    /// The URL of the authorization endpoint to use in the discovery document if <see cref="RegistrationEndpointType"/> is set to <see cref="RegistrationEndpointType.Static"/>.
    /// </value>
    public Uri? CustomRegistrationEndpoint { get; set; }
}
