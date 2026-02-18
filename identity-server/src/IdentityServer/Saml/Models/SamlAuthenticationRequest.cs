// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents contextual information about a SAML authentication request.
/// </summary>
public class SamlAuthenticationRequest
{
    /// <summary>
    /// Gets or sets the Service Provider making the authentication request.
    /// </summary>
    public required SamlServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// Gets or sets the original SAML AuthnRequest.
    /// Will be null for IdP-initiated SSO flows.
    /// </summary>
    public AuthNRequest? AuthNRequest { get; set; }

    /// <summary>
    /// Gets the requested authentication context from the AuthNRequest.
    /// This is a convenience property that accesses AuthNRequest.RequestedAuthnContext.
    /// </summary>
    public RequestedAuthnContext? RequestedAuthnContext => AuthNRequest?.RequestedAuthnContext;

    /// <summary>
    /// Gets or sets the RelayState parameter to be echoed back to the Service Provider.
    /// For IdP-initiated SSO, this typically contains the target URL at the SP.
    /// </summary>
    public string? RelayState { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an IdP-initiated SSO flow.
    /// If true, there was no AuthnRequest and the response will be unsolicited.
    /// </summary>
    public bool IsIdpInitiated { get; set; }
}
