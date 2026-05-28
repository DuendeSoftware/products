// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Extension methods for Saml2NameId
    /// </summary>
    internal static class Saml2NameIdExtensions
    {
        /// <summary>
        /// Create XElement for the Saml2NameIdentifier.
        /// </summary>
        /// <param name="nameIdentifier"></param>
        /// <returns></returns>
        public static XElement ToXElement(this Saml2NameIdentifier nameIdentifier)
        {
            if (nameIdentifier == null)
            {
                throw new ArgumentNullException(nameof(nameIdentifier));
            }

            var nameIdElement = new XElement(Saml2Namespaces.Saml2 + "NameID",
                            nameIdentifier.Value);
            nameIdElement.AddAttributeIfNotNullOrEmpty("Format", nameIdentifier.Format);
            nameIdElement.AddAttributeIfNotNullOrEmpty("NameQualifier", nameIdentifier.NameQualifier);
            nameIdElement.AddAttributeIfNotNullOrEmpty("SPNameQualifier", nameIdentifier.SPNameQualifier);
            nameIdElement.AddAttributeIfNotNullOrEmpty("SPProvidedID", nameIdentifier.SPProvidedId);

            return nameIdElement;
        }
    }
}
