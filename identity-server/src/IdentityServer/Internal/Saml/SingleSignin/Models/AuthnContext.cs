// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents a SAML 2.0 AuthnContext element
/// </summary>
internal record AuthnContext
{
    /// <summary>
    /// Authentication context class reference (URI)
    /// </summary>
    public string? AuthnContextClassRef { get; set; }
}
