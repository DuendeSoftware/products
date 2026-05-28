// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Configuration;

/// <summary>
/// SAML 2.0 binding types for authentication requests.
/// </summary>
public enum SamlBindingType
{
    /// <summary>
    /// HTTP-Redirect binding (SAML bindings section 3.4).
    /// </summary>
    HttpRedirect = 1,

    /// <summary>
    /// HTTP-POST binding (SAML bindings section 3.5).
    /// </summary>
    HttpPost = 2,
}
