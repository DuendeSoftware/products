// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Flat representation of the relevant fields from an AuthnRequest, suitable for storage.
/// Only the fields consumed after the login redirect are preserved here; the full
/// <c>AuthnRequest</c> remains available on the SSO endpoint path via
/// <c>ValidatedAuthnRequest.AuthnRequest</c>.
/// </summary>
public record StoredAuthnRequestData
{
    /// <summary>
    /// The original request ID (used for InResponseTo).
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Whether force re-authentication was requested.
    /// </summary>
    public bool ForceAuthn { get; init; }

    /// <summary>
    /// Whether passive authentication was requested.
    /// </summary>
    public bool IsPassive { get; init; }

    /// <summary>
    /// The NameIdPolicy Format from the original request.
    /// </summary>
    public string? NameIdPolicyFormat { get; init; }

    /// <summary>
    /// The Subject NameID value (login hint).
    /// </summary>
    public string? SubjectNameIdValue { get; init; }

    /// <summary>
    /// The IdP hint provider ID extracted from Scoping/IDPList.
    /// Only populated when the IDPList contains exactly one entry.
    /// </summary>
    public string? IdpHintProviderId { get; init; }

    /// <summary>
    /// The requested authentication context, if present.
    /// </summary>
    public StoredRequestedAuthnContext? RequestedAuthnContext { get; init; }
}

/// <summary>
/// Flat representation of a RequestedAuthnContext suitable for storage.
/// </summary>
public record StoredRequestedAuthnContext
{
    /// <summary>
    /// The comparison method (exact, minimum, maximum, better). Null if not specified.
    /// </summary>
    public string? Comparison { get; init; }

    /// <summary>
    /// Authentication context class references.
    /// </summary>
    public List<string> AuthnContextClassRef { get; init; } = [];

    /// <summary>
    /// Authentication context declaration references.
    /// </summary>
    public List<string> AuthnContextDeclRef { get; init; } = [];
}
