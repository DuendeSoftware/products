// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Metadata;

/// <summary>
/// Saml2 Endpoint Type.
/// </summary>
public class Endpoint
{
    /// <summary>
    /// Binding supported by the endpoint.
    /// </summary>
    public string Binding { get; set; } = "";

    /// <summary>
    /// URL of the endpoint.
    /// </summary>
    public string Location { get; set; } = "";
}
