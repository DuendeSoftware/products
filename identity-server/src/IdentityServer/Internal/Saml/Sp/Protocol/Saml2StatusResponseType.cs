// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Abstract Saml2 StatusResponseType class.
    /// </summary>
    internal abstract class Saml2StatusResponseType : ISaml2Message
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="status">Status of the response</param>
        protected Saml2StatusResponseType(Saml2StatusCode status)
        {
            Status = status;
        }

        /// <summary>
        /// The destination of the message.
        /// </summary>
        public Uri DestinationUrl { get; set; }

        /// <summary>
        /// Issuer of the message.
        /// </summary>
        public EntityId Issuer { get; set; }

        /// <summary>
        /// Id of request message, if this message is a response to a previous
        /// request.
        /// </summary>
        public Saml2Id InResponseTo { get; set; }

        /// <summary>
        /// The name of the message to use in a query string or form input
        /// field. Typically "SAMLRequest" or "SAMLResponse".
        /// </summary>
        public string MessageName { get; } = "SAMLResponse";

        /// <summary>
        /// RelayState attached to the message.
        /// </summary>
        /// <remarks>Strictly speaking, this is not part of the message,
        /// but it is delivered together with the message so we need to keep
        /// track of it together with a message.</remarks>
        public string RelayState { get; set; }

        /// <summary>
        /// Certificate used to sign the message with during binding, according
        /// to the signature processing rules of each binding.
        /// </summary>
        public X509Certificate2 SigningCertificate { get; set; }

        /// <summary>
        /// The signing algorithm to use when signing the message during binding, 
        /// according to the signature processing rules of each binding.
        /// </summary>
        /// <value>The signing algorithm.</value>
        public string SigningAlgorithm { get; set; }

        /// <summary>
        /// Status code of the message.
        /// </summary>
        public Saml2StatusCode Status { get; }

        /// <summary>
        /// Id of the message.
        /// </summary>
        public Saml2Id Id { get; set; } = new Saml2Id();

        /// <summary>
        /// Issue instant.
        /// </summary>
        public DateTime IssueInstant { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Serializes the message into wellformed Xml.
        /// </summary>
        /// <returns>string containing the Xml data.</returns>
        public abstract string ToXml();

        /// <summary>
        /// Transforms the message to an XElement object tree.
        /// </summary>
        /// <returns>XElement with Xml representation of the message</returns>
        public abstract XElement ToXElement();

        protected void ReadBaseProperties(XmlElement xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            if (xml.Attributes["Version"].Value != "2.0")
            {
                throw new XmlException("Wrong or unsupported SAML2 version");
            }

            Id = new Saml2Id(xml.Attributes["ID"].Value);

            var destination = xml.Attributes["Destination"];
            if (destination != null)
            {
                DestinationUrl = new Uri(destination.Value);
            }

            var issuerNode = xml["Issuer", Saml2Namespaces.Saml2Name];
            if (issuerNode != null)
            {
                Issuer = new EntityId(issuerNode.InnerXml);
            }

            var inResponseTo = xml.GetAttribute("InResponseTo");
            if (inResponseTo != null)
            {
                InResponseTo = new Saml2Id(inResponseTo);
            }

            var issueInstant = xml.GetAttribute("IssueInstant");
            if (issueInstant != null)
            {
                IssueInstant = DateTime.Parse(issueInstant, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            }
        }
    }
}
