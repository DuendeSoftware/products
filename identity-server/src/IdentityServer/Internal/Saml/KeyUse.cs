// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml;

/// <summary>
/// Represents the usage type of a SAML key descriptor.
/// </summary>
internal enum KeyUse
{
    /// <summary>
    /// Key used for signing.
    /// </summary>
    Signing,

    /// <summary>
    /// Key used for encryption.
    /// </summary>
    Encryption
}
