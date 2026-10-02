// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Specifies the signing behavior for outgoing SAML AuthnRequest messages.
/// <see cref="Never" /> is the default.
/// </summary>
public enum AuthnRequestSigningBehavior
{
    /// <summary>
    /// Never sign the AuthnRequest.
    /// </summary>
    Never = 0,

    /// <summary>
    /// Always sign the AuthnRequest.
    /// </summary>
    Always = 1
}
