// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Represents a SAML endpoint with location and binding.
/// </summary>
public class SamlEndpointType
{
    /// <summary>
    /// Gets or sets the URL of the endpoint.
    /// </summary>
    public Uri Location { get; set; } = default!;

    /// <summary>
    /// Gets or sets the SAML binding used by the endpoint.
    /// </summary>
    public SamlBinding Binding { get; set; }
}
