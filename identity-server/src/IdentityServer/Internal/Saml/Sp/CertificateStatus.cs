// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Is this certificate for current or future use?
    /// </summary>
    internal enum CertificateStatus
    {
        /// <summary>
        /// The certificate is used for current requests
        /// </summary>
        Current = 0,

        /// <summary>
        /// The certificate is used for current and/or future requests
        /// </summary>
        Future = 1
    }
}
