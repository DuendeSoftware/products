// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Represents an indexed SAML endpoint (e.g., ACS URL) with location, binding, index, and default indicator.
/// </summary>
public class SamlIndexedEndpointConfiguration
{
    /// <summary>
    /// The URL of the endpoint.
    /// </summary>
    public required string Location { get; set; }

    /// <summary>
    /// The SAML binding used by the endpoint.
    /// </summary>
    public SamlBinding Binding { get; set; }

    /// <summary>
    /// The index of the endpoint.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Whether this is the default endpoint.
    /// </summary>
    public bool IsDefault { get; set; }
}
