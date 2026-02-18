// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents a SAML 2.0 AuthnStatement element
/// </summary>
internal record AuthnStatement
{
    /// <summary>
    /// Time at which the authentication took place
    /// </summary>
    public required DateTime AuthnInstant { get; set; }

    /// <summary>
    /// Session index for the authenticated session
    /// </summary>
    public string? SessionIndex { get; set; }

    /// <summary>
    /// Time instant at which the session expires
    /// </summary>
    public DateTime? SessionNotOnOrAfter { get; set; }

    /// <summary>
    /// Authentication context
    /// </summary>
    public AuthnContext? AuthnContext { get; set; }
}
