// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Represents a SAML endpoint with location and binding for admin operations.
/// </summary>
public class SamlEndpointConfiguration
{
    /// <summary>
    /// The URL of the endpoint.
    /// </summary>
    public required string Location { get; set; }

    /// <summary>
    /// The SAML binding used by the endpoint.
    /// </summary>
    public SamlBinding Binding { get; set; }
}
