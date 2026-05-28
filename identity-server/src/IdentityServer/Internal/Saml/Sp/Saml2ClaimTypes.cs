// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Claim type constants.
    /// </summary>
    internal static class Saml2ClaimTypes
    {
        internal const string ClaimTypeNamespace = "http://Sustainsys.se/Saml2";

        /// <summary>
        /// Session index is set by the idp and is used to correlate sessions
        /// during single logout.
        /// </summary>
        public const string SessionIndex = ClaimTypeNamespace + "/SessionIndex";

        /// <summary>
        /// Original subject name identifier from the SAML2 idp, that should
        /// be logged out as part of a single logout scenario.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public const string LogoutNameIdentifier = ClaimTypeNamespace + "/LogoutNameIdentifier";
    }
}
