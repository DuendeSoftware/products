// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// The level of trust that a certain piece of data comes with.
    /// </summary>
    internal enum TrustLevel
    {
        /// <summary>
        /// The data cannot be trusted at all.
        /// </summary>
        None = 0,

        /// <summary>
        /// The data was retrieved through a request that was initiated from
        /// our end, but there was no transport protection.
        /// </summary>
        HttpGet = 100,

        /// <summary>
        /// The data was retrieved through a TLS-protected request that was
        /// initiated from our end, to a host that had a valid TLS certificate.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Tls")]
        TlsTransport = 200,

        /// <summary>
        /// The data was signed and has been verified by a signing key.
        /// </summary>
        Signature = 300,

        /// <summary>
        /// Data is from a local configuration source. E.g. metadata or a
        /// certificate loaded from disk.
        /// </summary>
        LocalConfiguration = 1000
    }
}
