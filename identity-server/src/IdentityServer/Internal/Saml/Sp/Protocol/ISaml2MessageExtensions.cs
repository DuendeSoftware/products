// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    static class Saml2MessageExtensions
    {
        extension<TMessage>(TMessage message)
            where TMessage : ISaml2Message
        {
            /// <summary>
            /// Serializes the message into wellformed XML.
            /// </summary>
            /// <param name="xmlCreatedNotification">Notification allowing modification of XML tree before serialization.</param>
            /// <returns>string containing the Xml data.</returns>
            public string ToXml(Action<XDocument> xmlCreatedNotification)
            {
                var xDocument = new XDocument(message.ToXElement());

                xmlCreatedNotification(xDocument);

                return xDocument.ToStringWithXmlDeclaration();
            }
        }
    }
}
