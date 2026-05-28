// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml;
using System.Xml.Linq;

using Duende.IdentityServer.Internal.Saml.Sp.Helpers;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// A Saml2 Logout Response.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
    internal class Saml2LogoutResponse : Saml2StatusResponseType
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="status">Status of the response.</param>
        public Saml2LogoutResponse(Saml2StatusCode status)
            : base(status)
        { }

        /// <summary>
        /// Serializes the message into wellformed Xml.
        /// </summary>
        /// <returns>string containing the Xml data.</returns>
        public override string ToXml()
        {
            var doc = XmlHelpers.CreateSafeXmlDocument();
            AppendTo(doc);
            return doc.DocumentElement.OuterXml;
        }

        public override XElement ToXElement()
        {
            var xe = new XElement(Saml2Namespaces.Saml2P + "LogoutResponse",
                new XAttribute("ID", Id.Value),
                new XAttribute("Version", "2.0"),
                new XAttribute("IssueInstant", IssueInstant.ToSaml2DateTimeString()),
                new XElement(Saml2Namespaces.Saml2P + "Status",
                    new XElement(Saml2Namespaces.Saml2P + "StatusCode",
                    new XAttribute("Value", StatusCodeHelper.FromCode(Status)))));

            if (Issuer != null)
            {
                xe.AddFirst(new XElement(Saml2Namespaces.Saml2 + "Issuer", Issuer.Id));
            }

            xe.AddAttributeIfNotNullOrEmpty("InResponseTo", InResponseTo?.Value);
            xe.AddAttributeIfNotNullOrEmpty("Destination", DestinationUrl?.OriginalString);

            return xe;
        }

        /// <summary>
        /// Appends xml for the Saml2LogoutResponse to the given parent node.
        /// </summary>
        /// <param name="parentNode">Xml for the Saml2LogoutResponse is appended
        /// to the children of this node.</param>
        public void AppendTo(XmlNode parentNode)
        {
            parentNode.StartElement("LogoutResponse", Saml2Namespaces.Saml2PUri)
                .AddAttribute("ID", Id.Value)
                .AddAttribute("Version", "2.0")
                .AddAttribute("IssueInstant", IssueInstant.ToSaml2DateTimeString())
                .AddAttributeIfNotNull("InResponseTo", InResponseTo?.Value)
                .AddAttributeIfNotNull("Destination", DestinationUrl?.OriginalString)
                .If(Issuer != null, x => x.AddElement("Issuer", Saml2Namespaces.Saml2Uri, Issuer.Id))
                .StartElement("Status", Saml2Namespaces.Saml2PUri)
                    .StartElement("StatusCode", Saml2Namespaces.Saml2PUri)
                    .AddAttribute("Value", StatusCodeHelper.FromCode(Status));
        }

        /// <summary>
        /// Load values into Saml2LogoutResponse from passed xml element
        /// </summary>
        /// <param name="xml">XmlElement containing a LogoutResponse</param>
        /// <returns>Saml2LogoutResponse</returns>
        public static Saml2LogoutResponse FromXml(XmlElement xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            var status = StatusCodeHelper.FromString(
                xml["Status", Saml2Namespaces.Saml2PName]
                ["StatusCode", Saml2Namespaces.Saml2PName]
                .GetAttribute("Value"));

            var response = new Saml2LogoutResponse(status);
            response.ReadBaseProperties(xml);

            return response;
        }
    }
}
