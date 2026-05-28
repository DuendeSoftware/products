// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Represents the stored context for a SAML authentication flow.
/// </summary>
public record SamlAuthenticationState
{
    /// <summary>
    /// Gets or sets the stored AuthnRequest data.
    /// Will be null for IdP-initiated SSO flows.
    /// </summary>
    public StoredAuthnRequestData? AuthnRequestData { get; set; }

    /// <summary>
    /// Gets or sets the entity ID of the service provider that initiated the request.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the assertion consumer service endpoint where the SAML response will be sent.
    /// </summary>
    public required IndexedEndpoint AssertionConsumerService { get; set; }

    /// <summary>
    /// Gets or sets the claim types to include in the SAML assertion.
    /// Determined during validation from the SP's AllowedScopes and RequestedClaimTypes configuration.
    /// </summary>
    public IReadOnlyList<string> RequestedClaimTypes { get; init; } = [];

    /// <summary>
    /// Gets or sets the UTC time at which this state entry expires.
    /// After this time, the state is considered invalid and may be cleaned up.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the interaction error when the user has denied or cancelled authentication.
    /// When set, the callback endpoint will generate a SAML error response instead of an assertion.
    /// </summary>
    public InteractionError? DenialError { get; set; }

    /// <summary>
    /// Gets or sets an optional human-readable description of the denial error.
    /// </summary>
    public string? DenialErrorDescription { get; set; }
}
