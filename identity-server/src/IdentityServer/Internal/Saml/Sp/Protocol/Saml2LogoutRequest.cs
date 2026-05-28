// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml;
using System.Xml.Linq;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// A Saml2 LogoutRequest message (SAML core spec 3.7.1)
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
    internal class Saml2LogoutRequest : Saml2RequestBase
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public Saml2LogoutRequest()
        {
            RelayState = SecureKeyGenerator.CreateRelayState();
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="id">Id of message.</param>
        public Saml2LogoutRequest(Saml2Id id)
        {
            Id = id;
        }

        /// <summary>
        /// Create Saml2LogoutRequest from data in Xml.
        /// </summary>
        /// <param name="xml">Xml data to initialize the Saml2LogoutRequest from.</param>
        public static Saml2LogoutRequest FromXml(XmlElement xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            var request = new Saml2LogoutRequest()
            {
                NameId = new Saml2NameIdentifier(
                    xml["NameID", Saml2Namespaces.Saml2Name].InnerText),
                RelayState = null
            };

            request.ReadBaseProperties(xml);

            var format = xml["NameID", Saml2Namespaces.Saml2Name].GetAttribute("Format");
            if (!string.IsNullOrEmpty(format))
            {
                request.NameId.Format = new Uri(format);
            }

            var sessionIndexElement = xml["SessionIndex", Saml2Namespaces.Saml2PName];
            if (sessionIndexElement != null)
            {
                request.SessionIndex = sessionIndexElement.InnerText;
            }

            return request;
        }

        /// <summary>
        /// The SAML2 request name
        /// </summary>
        protected override string LocalName
        {
            get
            {
                return "LogoutRequest";
            }
        }

        /// <summary>
        /// Name id to logout.
        /// </summary>
        public Saml2NameIdentifier NameId { get; set; }

        /// <summary>
        /// Session index to logout.
        /// </summary>
        public string SessionIndex { get; set; }

        /// <summary>
        /// Serializes the message into wellformed Xml.
        /// </summary>
        /// <returns>string containing the Xml data.</returns>
        public override string ToXml()
        {
            return ToXElement().ToString();
        }

        public override XElement ToXElement()
        {
            var x = new XElement(Saml2Namespaces.Saml2P + LocalName);

            x.Add(base.ToXNodes());
            x.Add(NameId.ToXElement());

            x.Add(new XElement(Saml2Namespaces.Saml2P + "SessionIndex",
                SessionIndex));

            return x;
        }
    }
}
