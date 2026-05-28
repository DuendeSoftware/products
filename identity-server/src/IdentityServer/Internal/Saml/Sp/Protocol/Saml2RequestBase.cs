// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Base class for saml requests, corresponds to section 3.2.1 in SAML Core specification.
    /// </summary>
    internal abstract class Saml2RequestBase : ISaml2Message
    {
        /// <summary>
        /// The id of the request.
        /// </summary>
        public Saml2Id Id { get; protected set; } = new Saml2Id("id" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Version of the SAML request. Always returns "2.0"
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public string Version
        {
            get
            {
                return "2.0";
            }
        }

        /// <summary>
        /// The instant that the request was issued (well actually, created).
        /// </summary>
        public string IssueInstant { get; } = DateTime.UtcNow.ToSaml2DateTimeString();

        /// <summary>
        /// SAML message name for requests - hard coded to SAMLRequest.
        /// </summary>
        public string MessageName
        {
            get
            {
                return "SAMLRequest";
            }
        }

        /// <summary>
        /// The destination of the request.
        /// </summary>
        public Uri DestinationUrl { get; set; }

        /// <summary>
        /// The issuer of the request.
        /// </summary>
        public EntityId Issuer { get; set; }

        /// <summary>
        /// The additional content to append within an Extensions element.
        /// </summary>
        public List<XElement> ExtensionContents { get; } = new List<XElement>();

        /// <summary>
        /// The SAML2 request name
        /// </summary>
        protected abstract string LocalName { get; }

        /// <summary>
        /// Transforms the message to an XElement object tree.
        /// </summary>
        /// <returns>XElement with Xml representation of the message</returns>
        public abstract XElement ToXElement();

        /// <summary>
        /// Creates XNodes for the fields of the Saml2RequestBase class. These
        /// nodes should be added when creating XML out of derived classes.
        /// </summary>
        /// <returns></returns>
        protected IEnumerable<XObject> ToXNodes()
        {
            yield return new XAttribute(XNamespace.Xmlns + "saml2p", Saml2Namespaces.Saml2PName);
            yield return new XAttribute(XNamespace.Xmlns + "saml2", Saml2Namespaces.Saml2Name);
            yield return new XAttribute("ID", Id.Value);
            yield return new XAttribute("Version", Version);
            yield return new XAttribute("IssueInstant", IssueInstant);

            if (DestinationUrl != null)
            {
                yield return new XAttribute("Destination", DestinationUrl.OriginalString);
            }

            if (Issuer != null && !string.IsNullOrEmpty(Issuer.Id))
            {
                yield return new XElement(Saml2Namespaces.Saml2 + "Issuer", Issuer.Id);
            }

            if (ExtensionContents != null && ExtensionContents.Count > 0)
            {
                yield return new XElement(Saml2Namespaces.Saml2P + "Extensions", ExtensionContents);
            }
        }

        /// <summary>
        /// Reads the request properties present in Saml2RequestBase
        /// Also validates basic properties of the request
        /// </summary>
        /// <param name="xml">The xml document to parse</param>
        protected void ReadBaseProperties(XmlElement xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }
            ValidateCorrectDocument(xml);
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

            var extensionsNode = xml["Extensions", Saml2Namespaces.Saml2PName];
            if (extensionsNode != null && extensionsNode.HasChildNodes)
            {
                XElement converted = XElement.Parse(extensionsNode.OuterXml);
                ExtensionContents.Clear();
                ExtensionContents.AddRange(converted.Elements());
            }
        }

        private void ValidateCorrectDocument(XmlElement xml)
        {
            if (xml.LocalName != LocalName
               || xml.NamespaceURI != Saml2Namespaces.Saml2P)
            {
                throw new XmlException("Expected a SAML2 authentication request document");
            }

            if (xml.Attributes["Version"].Value != "2.0")
            {
                throw new XmlException("Wrong or unsupported SAML2 version");
            }
        }

        /// <summary>
        /// Serializes the message into wellformed Xml.
        /// </summary>
        /// <returns>string containing the Xml data.</returns>
        public abstract string ToXml();

        /// <summary>
        /// RelayState attached to the message.
        /// </summary>
        public string RelayState { get; protected set; }

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
    }
}
