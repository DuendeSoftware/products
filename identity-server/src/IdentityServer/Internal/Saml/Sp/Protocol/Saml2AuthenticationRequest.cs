// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// An authentication request corresponding to section 3.4.1 in SAML Core specification.
    /// </summary>
    internal class Saml2AuthenticationRequest : Saml2RequestBase
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public Saml2AuthenticationRequest()
        {
            RelayState = SecureKeyGenerator.CreateRelayState();
        }

        /// <summary>
        /// The SAML2 request name
        /// </summary>
        protected override string LocalName
        {
            get { return "AuthnRequest"; }
        }

        /// <summary>
        /// Serializes the request to a Xml message.
        /// </summary>
        /// <returns>XElement</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Lowercase demanded by specification.")]
        public override XElement ToXElement()
        {
            var x = new XElement(Saml2Namespaces.Saml2P + LocalName);

            x.Add(base.ToXNodes());
            if (Binding.HasValue)
            {
                x.AddAttributeIfNotNullOrEmpty("ProtocolBinding", Saml2Binding.Saml2BindingTypeToUri(Binding.Value));
            }
            x.AddAttributeIfNotNullOrEmpty("AssertionConsumerServiceURL", AssertionConsumerServiceUrl);
            x.AddAttributeIfNotNullOrEmpty("AttributeConsumingServiceIndex", AttributeConsumingServiceIndex);
            if (ForceAuthentication)
            {
                x.Add(new XAttribute("ForceAuthn", ForceAuthentication));
            }
            if (IsPassive)
            {
                x.Add(new XAttribute("IsPassive", IsPassive));
            }

            AddNameIdPolicy(x);

            if (RequestedAuthnContext != null && RequestedAuthnContext.ClassRef != null)
            {
                x.Add(new XElement(Saml2Namespaces.Saml2P + "RequestedAuthnContext",
                    new XAttribute("Comparison", RequestedAuthnContext.Comparison.ToString().ToLowerInvariant()),

                    // Add the classref as original string to avoid URL normalization
                    // and make sure the emitted value is exactly the configured.
                    new XElement(Saml2Namespaces.Saml2 + "AuthnContextClassRef",
                        RequestedAuthnContext.ClassRef.OriginalString)));
            }

            AddScoping(x);

            return x;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "NameIdPolicy")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AllowCreate")]
        private void AddNameIdPolicy(XElement xElement)
        {
            if (NameIdPolicy != null &&
                (NameIdPolicy.AllowCreate.HasValue || NameIdPolicy.Format != NameIdFormat.NotConfigured))
            {
                if (NameIdPolicy.AllowCreate.HasValue && NameIdPolicy.Format == NameIdFormat.Transient)
                {
                    throw new InvalidOperationException("When NameIdPolicy/Format is set to Transient, it is not permitted to specify AllowCreate. Change Format or leave AllowCreate as null.");
                }

                var nameIdPolicyElement = new XElement(Saml2Namespaces.Saml2P + "NameIDPolicy");

                if (NameIdPolicy.Format != NameIdFormat.NotConfigured)
                {
                    nameIdPolicyElement.Add(new XAttribute("Format",
                        NameIdPolicy.Format.GetUri()));
                }

                if (NameIdPolicy.AllowCreate.HasValue)
                {
                    nameIdPolicyElement.Add(new XAttribute("AllowCreate",
                        NameIdPolicy.AllowCreate));
                }

                xElement.Add(nameIdPolicyElement);
            }
        }

        private void AddScoping(XElement xElement)
        {
            if (Scoping != null)
            {
                xElement.Add(Scoping.ToXElement());
            }
        }

        /// <summary>
        /// Serializes the message into wellformed Xml
        /// </summary>
        /// <returns>string containing the Xml data.</returns>
        public override string ToXml()
        {
            return ToXElement().ToString();
        }

        /// <summary>
        /// Read the supplied Xml and parse it into a authenticationrequest.
        /// </summary>
        /// <param name="xml">xml data.</param>
        /// <param name="relayState">Relay State attached to the message or null if not present.</param>
        /// <returns>Saml2Request</returns>
        /// <exception cref="XmlException">On xml errors or unexpected xml structure.</exception>
        public static Saml2AuthenticationRequest Read(string xml, string relayState)
        {
            if (xml == null)
            {
                return null;
            }
            var x = XmlHelpers.XmlDocumentFromString(xml);

            return new Saml2AuthenticationRequest(x.DocumentElement, relayState);
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="xml">Xml data</param>
        /// <param name="relayState">RelayState associateed with the message.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.Enum.TryParse<Sustainsys.Saml2.Saml2P.NameIdFormat>(System.String,System.Boolean,Sustainsys.Saml2.Saml2P.NameIdFormat@)")]
        public Saml2AuthenticationRequest(XmlElement xml, string relayState)
        {
            ReadBaseProperties(xml);
            RelayState = relayState;

            var AssertionConsumerServiceUriString = xml.Attributes["AssertionConsumerServiceURL"].GetValueIfNotNull();

            if (AssertionConsumerServiceUriString != null)
            {
                AssertionConsumerServiceUrl = new Uri(AssertionConsumerServiceUriString);
            }

            var forceAuthnString = xml.Attributes["ForceAuthn"].GetValueIfNotNull();
            if (forceAuthnString != null)
            {
                ForceAuthentication = XmlConvert.ToBoolean(forceAuthnString);
            }

            var isPassiveString = xml.Attributes["IsPassive"].GetValueIfNotNull();
            if (isPassiveString != null)
            {
                IsPassive = XmlConvert.ToBoolean(isPassiveString);
            }

            var node = xml["NameIDPolicy", Saml2Namespaces.Saml2PName];
            if (node != null)
            {
                var fullFormat = node.Attributes["Format"].GetValueIfNotNull();
                var format = fullFormat?.Split(':').LastOrDefault();
                NameIdFormat nameIdFormat = NameIdFormat.NotConfigured;
                if (format != null)
                {
                    Enum.TryParse(format, true, out nameIdFormat);
                }

                bool? allowCreate = null;
                var allowCreateStr = node.Attributes["AllowCreate"].GetValueIfNotNull();
                if (allowCreateStr != null)
                {
                    allowCreate = XmlConvert.ToBoolean(allowCreateStr);
                }

                NameIdPolicy = new Saml2NameIdPolicy(allowCreate, nameIdFormat);
            }
        }

        /// <summary>
        /// The assertion consumer url that the idp should send its response back to.
        /// </summary>
        public Uri AssertionConsumerServiceUrl { get; set; }

        /// <summary>
        /// Index to the SP metadata where the list of requested attributes is found.
        /// </summary>
        public int? AttributeConsumingServiceIndex { get; set; }

        /// <summary> 
        /// Scoping for request 
        /// </summary> 
        public Saml2Scoping Scoping { get; set; }

        /// <summary>
        /// NameId policy.
        /// </summary>
        public Saml2NameIdPolicy NameIdPolicy { get; set; }

        /// <summary>
        /// RequestedAuthnContext.
        /// </summary>
        public Saml2RequestedAuthnContext RequestedAuthnContext { get; set; }

        /// <summary>
        /// Binding type to request the Idp to use when responding.
        /// </summary>
        public Saml2BindingType? Binding { get; set; }

        /// <summary>
        /// Sets whether request should force the idp to authenticate the presenter directly, 
        /// rather than rely on a previous security context.
        /// If false, the ForceAuthn parameter is omitted from the request.
        /// If true, the request is sent with ForceAuthn="true".
        /// </summary>
        public bool ForceAuthentication { get; set; } = false;

        /// <summary>
        /// Sets whether request should request for SAML Passive login if possible, 
        /// If false, the IsPassive parameter is omitted from the request.
        /// If true, the request is sent with IsPassive="true".
        /// </summary>
        public bool IsPassive { get; set; } = false;
    }
}
