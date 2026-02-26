// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Represents the available SAML protocol bindings for message transport.
/// </summary>
public enum SamlBinding
{
    /// <summary>
    /// HTTP-Redirect binding.
    /// </summary>
    HttpRedirect,

    /// <summary>
    /// HTTP-POST binding.
    /// </summary>
    HttpPost
}
