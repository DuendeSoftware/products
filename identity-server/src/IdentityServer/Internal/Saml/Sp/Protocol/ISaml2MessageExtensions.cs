// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    static class Saml2MessageExtensions
    {
        /// <summary>
        /// Serializes the message into wellformed XML.
        /// </summary>
        /// <param name="message">Saml2 message to transform to XML</param>
        /// <param name="xmlCreatedNotification">Notification allowing modification of XML tree before serialization.</param>
        /// <returns>string containing the Xml data.</returns>
        public static string ToXml<TMessage>(
            this TMessage message, Action<XDocument> xmlCreatedNotification)
            where TMessage : ISaml2Message
        {
            var xDocument = new XDocument(message.ToXElement());

            xmlCreatedNotification(xDocument);

            return xDocument.ToStringWithXmlDeclaration();
        }
    }
}
