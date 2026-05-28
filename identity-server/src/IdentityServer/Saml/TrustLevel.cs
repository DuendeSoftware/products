// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml;

/// <summary>
/// What is the trust level of a piece of data? The levels reflect
/// how trustworthy the data is based on if it is signed and how
/// the signature can be validated.
/// </summary>
/// <remarks>
/// This is a flags enum. The <see cref="HasSignature"/> flag is ORed in when
/// a signature has been validated. The base trust level values use non-adjacent
/// bit positions to leave room for future extension values (0x2, 0x8, 0x20).
/// </remarks>
[Flags]
public enum TrustLevel
{
    /// <summary>
    /// There is no integrity protection for the data.
    /// </summary>
    None = 0,

    /// <summary>
    /// A signature has been validated on the data. This flag is ORed into
    /// the base trust level when signature validation succeeds.
    /// </summary>
    HasSignature = 1,

    /// <summary>
    /// The data was retrieved over an outbound network connection,
    /// but the transport was not protected. This level is also set
    /// on all data that is verified as signed by a key that was retrieved
    /// over plain http.
    /// </summary>
    Http = 4,

    /// <summary>
    /// The data was directly retrieved from the source using a valid
    /// TLS (https) connection. This level is also set on all data that
    /// is verified as signed by a key that was retrieved over TLS/https.
    /// In most setups, this level is regarded as secure.
    /// </summary>
    TLS = 0x10,

    /// <summary>
    /// The data was verified by a signature where signing key or a strong
    /// identifier of the key (such as a SHA256 cert thumbprint) was read
    /// from configuration.
    /// </summary>
    ConfiguredKey = 0x40
}
