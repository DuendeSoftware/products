// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Claims;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Exceptions;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Somewhat ugly subclassing to be able to access some methods that are protected
    /// on Saml2SecurityTokenHandler. The public interface of Saml2SecurityTokenHandler
    /// expects the actual assertion to be signed, which is not always the case when
    /// using Saml2-P. The assertion can be embedded in a signed response. Or the signing
    /// could be handled at transport level.
    /// </summary>
    internal class Saml2PSecurityTokenHandler : Saml2SecurityTokenHandler
    {
        public Saml2PSecurityTokenHandler() : this(null)
        {
            // backward compatibility = null spOptions
        }

        public Saml2PSecurityTokenHandler(SPOptions spOptions)
        {
            Serializer = new Saml2PSerializer(spOptions);
        }

        // TODO: needed with Microsoft.identitymodel?
        /// <summary>
        /// Process authentication statement from SAML assertion. WIF chokes if the authentication statement 
        /// contains a DeclarationReference, so we clear this out before calling the base method
        /// http://referencesource.microsoft.com/#System.IdentityModel/System/IdentityModel/Tokens/Saml2SecurityTokenHandler.cs,1970
        /// </summary>
        /// <param name="statement">Authentication statement</param>
        /// <param name="subject">Claim subject</param>
        /// <param name="issuer">Assertion Issuer</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        protected override void ProcessAuthenticationStatement(Saml2AuthenticationStatement statement, ClaimsIdentity subject, string issuer)
        {
            if (statement.AuthenticationContext != null)
            {
                statement.AuthenticationContext.DeclarationReference = null;
            }
            base.ProcessAuthenticationStatement(statement, subject, issuer);

            if (statement.SessionIndex != null)
            {
                var nameIdClaim = subject.FindFirst(ClaimTypes.NameIdentifier);

                if (nameIdClaim != null)
                {
                    subject.AddClaim(
                        new Claim(
                            Saml2ClaimTypes.LogoutNameIdentifier,
                            DelimitedString.Join(
                                nameIdClaim.Properties.GetValueOrEmpty(ClaimProperties.SamlNameIdentifierNameQualifier),
                                nameIdClaim.Properties.GetValueOrEmpty(ClaimProperties.SamlNameIdentifierSPNameQualifier),
                                nameIdClaim.Properties.GetValueOrEmpty(ClaimProperties.SamlNameIdentifierFormat),
                                nameIdClaim.Properties.GetValueOrEmpty(ClaimProperties.SamlNameIdentifierSPProvidedId),
                                nameIdClaim.Value),
                            null,
                            issuer));
                }

                subject.AddClaim(
                    new Claim(Saml2ClaimTypes.SessionIndex, statement.SessionIndex, null, issuer));
            }
        }

        // Override and build our own logic. The problem is ValidateTokenReplay that serializes the token back. And that
        // breaks because it expects some optional values to be present.
        public override ClaimsPrincipal ValidateToken(string token, TokenValidationParameters validationParameters, out Microsoft.IdentityModel.Tokens.SecurityToken validatedToken)
        {
            var samlToken = ReadSaml2Token(token);

            ValidateConditions(samlToken, validationParameters);
            ValidateSubject(samlToken, validationParameters);

            var issuer = ValidateIssuer(samlToken.Issuer, samlToken, validationParameters);

            // Just using the assertion id for token replay. As that is part of the signed value it cannot
            // be altered by someone replaying the token.
            ValidateTokenReplay(samlToken.Assertion.Conditions.NotOnOrAfter, samlToken.Assertion.Id.Value, validationParameters);

            // ValidateIssuerSecurityKey not called - we have our own signature validation.

            validatedToken = samlToken;
            var identity = CreateClaimsIdentity(samlToken, issuer, validationParameters);

            if (validationParameters.SaveSigninToken)
            {
                identity.BootstrapContext = samlToken;
            }

            return new ClaimsPrincipal(identity);
        }
        private static readonly Uri bearerUri = new Uri("urn:oasis:names:tc:SAML:2.0:cm:bearer");

        protected override void ValidateSubject(Saml2SecurityToken samlToken, TokenValidationParameters validationParameters)
        {
            base.ValidateSubject(samlToken, validationParameters);

            if (!samlToken.Assertion.Subject.SubjectConfirmations.Any())
            {
                throw new Saml2ResponseFailedValidationException("No subject confirmation method found.");
            }

            if (!samlToken.Assertion.Subject.SubjectConfirmations.Any(sc => sc.Method == bearerUri))
            {
                throw new Saml2ResponseFailedValidationException("Only assertions with subject confirmation method \"bearer\" are supported.");
            }
        }
    }
}
