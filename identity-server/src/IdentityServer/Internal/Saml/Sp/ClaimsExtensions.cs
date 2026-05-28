// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Claims;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Extension methods for claims.
    /// </summary>
    internal static class ClaimsExtensions
    {
        /// <summary>
        /// Create a Saml2NameIdentifier from a claim.
        /// </summary>
        /// <param name="claim">Name identifier or Saml2 logout info claim.</param>
        /// <returns>Saml2NameIdentifier</returns>
        /// <remarks>The field order is:NameQualifier,SPNameQualifier,Format,SPProvidedID,Value</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "logout")]
        public static Saml2NameIdentifier ToSaml2NameIdentifier(this Claim claim)
        {
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            if (claim.Type == Saml2ClaimTypes.LogoutNameIdentifier)
            {
                return ProcessLogoutNameIdentifier(claim);
            }

            if (claim.Type == ClaimTypes.NameIdentifier)
            {
                return ProcessNameIdentifier(claim);
            }

            throw new ArgumentException("ToSaml2NameIdentifier can only process an Saml2 logout name identifier claim.", nameof(claim));
        }

        private static Saml2NameIdentifier ProcessLogoutNameIdentifier(Claim claim)
        {
            var fields = DelimitedString.Split(claim.Value);

            var saml2NameIdentifier = new Saml2NameIdentifier(fields[4]);

            if (!string.IsNullOrEmpty(fields[0]))
            {
                saml2NameIdentifier.NameQualifier = fields[0];
            }
            if (!string.IsNullOrEmpty(fields[1]))
            {
                saml2NameIdentifier.SPNameQualifier = fields[1];
            }
            if (!string.IsNullOrEmpty(fields[2]))
            {
                saml2NameIdentifier.Format = new Uri(fields[2]);
            }
            if (!string.IsNullOrEmpty(fields[3]))
            {
                saml2NameIdentifier.SPProvidedId = fields[3];
            }

            return saml2NameIdentifier;
        }

        private static Saml2NameIdentifier ProcessNameIdentifier(Claim claim)
        {
            var saml2NameIdentifier = new Saml2NameIdentifier(claim.Value);

            claim.ExtractProperty(ClaimProperties.SamlNameIdentifierFormat,
                value => saml2NameIdentifier.Format = new Uri(value));
            claim.ExtractProperty(ClaimProperties.SamlNameIdentifierNameQualifier,
                value => saml2NameIdentifier.NameQualifier = value);
            claim.ExtractProperty(ClaimProperties.SamlNameIdentifierSPNameQualifier,
                value => saml2NameIdentifier.SPNameQualifier = value);
            claim.ExtractProperty(ClaimProperties.SamlNameIdentifierSPProvidedId,
                value => saml2NameIdentifier.SPProvidedId = value);

            return saml2NameIdentifier;
        }

        private static void ExtractProperty(
            this Claim claim,
            string propertyKey,
            Action<string> propertySetter)
        {
            string value;
            if (claim.Properties.TryGetValue(propertyKey, out value))
            {
                propertySetter(value);
            }
        }
    }
}
