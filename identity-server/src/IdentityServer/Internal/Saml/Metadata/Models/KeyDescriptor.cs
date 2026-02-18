// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml.Metadata.Models;

/// <summary>
/// Describes a cryptographic key used by a SAML entity.
/// Contains certificate information for signature verification or encryption.
/// </summary>
internal record KeyDescriptor
{
    /// <summary>
    /// Gets or sets the key usage (signing, encryption, or null for both).
    /// When null, the key can be used for both signing and encryption.
    /// </summary>
    internal KeyUse? Use { get; set; }

    /// <summary>
    /// Gets or sets the X.509 certificate in base64 encoding (without BEGIN/END markers).
    /// This is the public key used to verify signatures or encrypt data.
    /// </summary>
    internal required string X509Certificate { get; set; }
}
