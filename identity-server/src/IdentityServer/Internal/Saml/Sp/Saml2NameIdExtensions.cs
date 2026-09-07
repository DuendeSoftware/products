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
        extension(Saml2NameIdentifier nameIdentifier)
        {
            /// <summary>
            /// Create XElement for the Saml2NameIdentifier.
            /// </summary>
            /// <returns></returns>
            public XElement ToXElement()
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
}
