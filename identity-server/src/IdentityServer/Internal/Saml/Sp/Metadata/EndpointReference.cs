// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class ServiceName
    {
        public string PortName { get; set; }
        public string Name { get; set; }
    }

    internal class EndpointReference
    {
        public Collection<XmlElement> Metadata { get; private set; } =
            new Collection<XmlElement>();
        public Collection<XmlElement> ReferenceProperties { get; private set; } =
            new Collection<XmlElement>();
        public Collection<XmlElement> ReferenceParameters { get; private set; } =
            new Collection<XmlElement>();
        public Collection<XmlElement> Policies { get; private set; } =
            new Collection<XmlElement>();
        public string PortType { get; set; }
        public ServiceName ServiceName { get; set; }
        public Uri Uri { get; internal set; }

        internal EndpointReference()
        {
        }

        public EndpointReference(string uri)
        {
            Uri = new Uri(uri);
        }
    }
}
