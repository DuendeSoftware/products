// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Extension methods for Saml2Assertion
    /// </summary>
    internal static class Saml2AssertionExtensions
    {
        /// <summary>
        /// Writes out the assertion as an XElement.
        /// </summary>
        /// <param name="assertion">The assertion to create xml for.</param>
        /// <returns>XElement</returns>
        public static XElement ToXElement(this Saml2Assertion assertion)
        {
            if (assertion == null)
            {
                throw new ArgumentNullException(nameof(assertion));
            }

            var xml = new XElement(Saml2Namespaces.Saml2 + "Assertion",
                new XAttribute(XNamespace.Xmlns + "saml2", Saml2Namespaces.Saml2Name),
                new XAttribute("Version", assertion.Version),
                new XAttribute("ID", assertion.Id.Value),
                new XAttribute("IssueInstant",
                    assertion.IssueInstant.ToSaml2DateTimeString()),
                new XElement(Saml2Namespaces.Saml2 + "Issuer", assertion.Issuer.Value));

            if (assertion.Subject != null)
            {
                xml.Add(assertion.Subject.ToXElement());
            }

            if (assertion.Conditions != null)
            {
                xml.Add(assertion.Conditions.ToXElement());
            }

            if (assertion.Statements != null)
            {
                foreach (var statement in assertion.Statements)
                {
                    xml.Add(statement.ToXElement());
                }
                ;
            }

            return xml;
        }
    }
}
