// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// Signing behavior for requests.
    /// </summary>
    internal enum SigningBehavior
    {
        /// <summary>
        /// Sign authnrequests if the idp is configured for it. This is the 
        /// default behavior.
        /// </summary>
        IfIdpWantAuthnRequestsSigned = 0,

        /// <summary>
        /// Always sign AuthnRequests. AuthnRequestsSigned is set to true
        /// in metadata.
        /// </summary>
        Always = 1,

        /// <summary>
        /// Never sign AuthnRequests.
        /// </summary>
        Never = 3,
    }
}
