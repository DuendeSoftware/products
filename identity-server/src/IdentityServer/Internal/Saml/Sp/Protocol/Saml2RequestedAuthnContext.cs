// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Configuration of RequestedAuthnContext
    /// </summary>
    internal class Saml2RequestedAuthnContext
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="classRef">AuthnContextClassRef</param>
        /// <param name="comparison">Comparison</param>
        public Saml2RequestedAuthnContext(Uri classRef, AuthnContextComparisonType comparison)
        {
            ClassRef = classRef;
            Comparison = comparison;
        }

        /// <summary>
        /// Authentication context class reference.
        /// </summary>
        public Uri ClassRef { get; }

        /// <summary>
        /// Comparison method.
        /// </summary>
        public AuthnContextComparisonType Comparison { get; }
    }
}
