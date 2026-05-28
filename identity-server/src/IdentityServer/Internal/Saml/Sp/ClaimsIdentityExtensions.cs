// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Claims;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Extension methods for Claims Identities
    /// </summary>
    internal static class ClaimsIdentityExtensions
    {
        /// <summary>
        /// Creates a Saml2Assertion from a ClaimsIdentity.
        /// </summary>
        /// <param name="identity">Claims to include in Assertion.</param>
        /// <param name="issuer">Issuer to include in assertion.</param>
        /// <returns>Saml2Assertion</returns>
        public static Saml2Assertion ToSaml2Assertion(this ClaimsIdentity identity, EntityId issuer)
        {
            return ToSaml2Assertion(identity, issuer, null);
        }

        /// <summary>
        /// Creates a Saml2Assertion from a ClaimsIdentity.
        /// </summary>
        /// <param name="identity">Claims to include in Assertion.</param>
        /// <param name="issuer">Issuer to include in assertion.</param>
        /// <param name="audience">Audience to set as audience restriction.</param>
        /// <returns>Saml2Assertion</returns>
        public static Saml2Assertion ToSaml2Assertion(
            this ClaimsIdentity identity,
            EntityId issuer,
            Uri audience)
        {
            return ToSaml2Assertion(identity, issuer, audience, null, null);
        }

        /// <summary>
        /// Creates a Saml2Assertion from a ClaimsIdentity.
        /// </summary>
        /// <param name="identity">Claims to include in Assertion.</param>
        /// <param name="issuer">Issuer to include in assertion.</param>
        /// <param name="audience">Audience to set as audience restriction.</param>
        /// <param name="inResponseTo">In response to id</param>
        /// <param name="destinationUri">The destination Uri for the message</param>
        /// <returns>Saml2Assertion</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Saml2Assertion ToSaml2Assertion(
            this ClaimsIdentity identity,
            EntityId issuer,
            Uri audience,
            Saml2Id inResponseTo,
            Uri destinationUri)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (issuer == null)
            {
                throw new ArgumentNullException(nameof(issuer));
            }

            var assertion = new Saml2Assertion(new Saml2NameIdentifier(issuer.Id));

            assertion.Statements.Add(
                new Saml2AuthenticationStatement(
                    new Saml2AuthenticationContext(
                        new Uri("urn:oasis:names:tc:SAML:2.0:ac:classes:unspecified")))
                {
                    SessionIndex = identity.Claims.SingleOrDefault(
                        c => c.Type == Saml2ClaimTypes.SessionIndex)?.Value
                });

            var attributeClaims = identity.Claims.Where(
                c => c.Type != ClaimTypes.NameIdentifier
                && c.Type != Saml2ClaimTypes.SessionIndex).GroupBy(c => c.Type)
                .ToArray();

            if (attributeClaims.Any())
            {
                assertion.Statements.Add(
                    new Saml2AttributeStatement(
                        attributeClaims.Select(
                            ac => new Saml2Attribute(ac.Key, ac.Select(c => c.Value)))));
            }

            var notOnOrAfter = DateTime.UtcNow.AddMinutes(2);

            Saml2SubjectConfirmationData confirmationData =
                new Saml2SubjectConfirmationData
                {
                    NotOnOrAfter = notOnOrAfter,
                    InResponseTo = inResponseTo
                };

            // Work around a bug in Microsoft.IdentityModel.Tokens.Saml2.Saml2SubjectConfirmationData
            // where the setter for Recipient throws an ArgumentNullException.  Recipient is optional
            // as per [Saml2Core, 2.4.1.2]
            if (destinationUri != null)
            {
                confirmationData.Recipient = destinationUri;
            }

            assertion.Subject = new Saml2Subject(identity.ToSaml2NameIdentifier())
            {
                SubjectConfirmations =
                {
                    new Saml2SubjectConfirmation(
                        new Uri("urn:oasis:names:tc:SAML:2.0:cm:bearer"),
                        confirmationData)
                }
            };

            assertion.Conditions = new Saml2Conditions()
            {
                NotOnOrAfter = notOnOrAfter
            };

            if (audience != null)
            {
                assertion.Conditions.AudienceRestrictions.Add(
                    new Saml2AudienceRestriction(audience.ToString()));
            }

            return assertion;
        }

        /// <summary>
        /// Create a Saml2NameIdentifier from the identity.
        /// </summary>
        /// <param name="identity">Identity to get NameIdentifier claim from.</param>
        /// <returns>Saml2NameIdentifier</returns>
        public static Saml2NameIdentifier ToSaml2NameIdentifier(this ClaimsIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return identity.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier)
                .ToSaml2NameIdentifier();
        }
    }
}
