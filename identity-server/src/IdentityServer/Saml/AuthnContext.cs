// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml;

/// <summary>
/// AuthnContext, Core 2.7.2.2
/// </summary>
public class AuthnContext
{
    /// <summary>
    /// Authentication Context Class Reference
    /// </summary>
    public string? AuthnContextClassRef { get; set; }

    /// <summary>
    /// Authentication context declaration
    /// </summary>
    public string? AuthnContextDecl { get; set; }

    /// <summary>
    /// Authentication Context Declaration Reference
    /// </summary>
    public string? AuthnContextDeclRef { get; set; }

    /// <summary>
    /// Authenticating Authorities
    /// </summary>
    public List<string> AuthenticatingAuthorities { get; } = [];
}
