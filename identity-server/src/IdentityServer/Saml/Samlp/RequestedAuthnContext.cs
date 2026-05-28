// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// Specifies the authentication Context requirements of authentication statements.
/// Element RequestedAuthContext, Core 3.3.2.2.1
/// </summary>
public class RequestedAuthnContext
{
    /// <summary>
    /// A comparison method.
    /// One of "exact", "minimum", "maximum" or "better". Default is "exact".
    /// </summary>
    public string Comparison { get; set; } = "";

    ///<summary>
    /// One or more authentication Context Class References.
    /// It describes the authentication context declarations that follows.
    /// </summary>
    public List<string> AuthnContextClassRef { get; } = [];

    /// <summary>
    /// One or more authentication Context Class References.
    /// Either an authentication context declaration provided by value or a URI reference.
    /// </summary>
    public List<string> AuthnContextDeclRef { get; } = [];
}
