// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Metadata;

/// <summary>
/// IDPSSODescriptor
/// </summary>
public class IDPSSODescriptor : SSODescriptor
{
    /// <summary>
    /// List of SingleSignOnService endpoints.
    /// </summary>
    public List<Endpoint> SingleSignOnServices { get; } = [];

    /// <summary>
    /// Does the Idp want any AuthnRequests to be signed?
    /// </summary>
    public bool? WantAuthnRequestsSigned { get; set; }
}
