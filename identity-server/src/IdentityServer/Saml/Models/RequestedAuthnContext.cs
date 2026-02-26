// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents the RequestedAuthnContext element from a SAML AuthnRequest.
/// Specifies requirements or preferences for the authentication context the IdP should use.
/// SAML 2.0 Core Section 3.3.2.2.1
/// </summary>
public record RequestedAuthnContext
{
    /// <summary>
    /// Gets the authentication context class references requested by the SP.
    /// URIs identifying authentication context classes (e.g., urn:oasis:names:tc:SAML:2.0:ac:classes:Password).
    /// </summary>
    public required IReadOnlyCollection<string> AuthnContextClassRefs { get; init; }

    /// <summary>
    /// Gets the comparison method to apply to the requested contexts.
    /// Default: Exact
    /// </summary>
    public AuthnContextComparison Comparison { get; init; } = AuthnContextComparison.Exact;

    internal static class ElementNames
    {
        public const string AuthnContextClassRef = "AuthnContextClassRef";
    }

    internal static class AttributeNames
    {
        public const string Comparison = "Comparison";
    }
}
