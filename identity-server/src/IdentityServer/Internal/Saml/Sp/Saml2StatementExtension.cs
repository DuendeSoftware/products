// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Extension methods for Saml2Statement
    /// </summary>
    internal static class Saml2StatementExtension
    {
        /// <summary>
        /// Writes out the statement as an XElement.
        /// </summary>
        /// <param name="statement">Statement to create xml for.</param>
        /// <returns>XElement</returns>
        public static XElement ToXElement(this Saml2Statement statement)
        {
            if (statement == null)
            {
                throw new ArgumentNullException(nameof(statement));
            }

            var attributeStatement = statement as Saml2AttributeStatement;
            if (attributeStatement != null)
            {
                return ToXElement(attributeStatement);
            }

            var authnStatement = statement as Saml2AuthenticationStatement;
            if (authnStatement != null)
            {
                return ToXElement(authnStatement);
            }

            throw new NotImplementedException("Statement of type " + statement.GetType().Name + " is not supported.");
        }

        private static XElement ToXElement(Saml2AuthenticationStatement authnStatement)
        {
            var result = new XElement(Saml2Namespaces.Saml2 + "AuthnStatement",
                new XAttribute("AuthnInstant", authnStatement.AuthenticationInstant.ToSaml2DateTimeString()),
                new XElement(Saml2Namespaces.Saml2 + "AuthnContext",
                    new XElement(Saml2Namespaces.Saml2 + "AuthnContextClassRef",
                        authnStatement.AuthenticationContext.ClassReference.OriginalString)));

            if (authnStatement.SessionIndex != null)
            {
                result.Add(new XAttribute("SessionIndex", authnStatement.SessionIndex));
            }

            return result;
        }

        private static XElement ToXElement(Saml2AttributeStatement attributeStatement)
        {
            var element = new XElement(Saml2Namespaces.Saml2 + "AttributeStatement");

            foreach (var attribute in attributeStatement.Attributes)
            {
                var attributeElement = new XElement(Saml2Namespaces.Saml2 + "Attribute", new XAttribute("Name", attribute.Name));

                attributeElement.AddAttributeIfNotNullOrEmpty("FriendlyName", attribute.FriendlyName);
                attributeElement.AddAttributeIfNotNullOrEmpty("NameFormat", attribute.NameFormat);
                attributeElement.AddAttributeIfNotNullOrEmpty("OriginalIssuer", attribute.OriginalIssuer);

                foreach (var value in attribute.Values)
                {
                    attributeElement.Add(new XElement(Saml2Namespaces.Saml2 + "AttributeValue", value));
                }

                element.Add(attributeElement);
            }

            return element;
        }
    }
}
