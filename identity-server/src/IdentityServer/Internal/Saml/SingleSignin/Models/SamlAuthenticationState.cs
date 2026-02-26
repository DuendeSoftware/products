// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents the stored context for a SAML authentication flow.
/// </summary>
internal record SamlAuthenticationState
{
    /// <summary>
    /// Gets or sets the original AuthnRequest.
    /// Will be null for IdP-initiated SSO flows.
    /// </summary>
    public AuthNRequest? Request { get; set; }

    public required string ServiceProviderEntityId { get; init; }

    /// <summary>
    /// Gets or sets the RelayState parameter from the original request.
    /// For IdP-initiated SSO, this typically contains the target URL at the SP.
    /// </summary>
    public string? RelayState { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an IdP-initiated SSO flow.
    /// If true, there was no AuthnRequest and the response will be unsolicited.
    /// </summary>
    public bool IsIdpInitiated { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this context was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    public required Uri AssertionConsumerServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the RequestedAuthnContext in the request were met.
    /// </summary>
    public bool RequestedAuthnContextRequirementsWereMet { get; set; }
}
