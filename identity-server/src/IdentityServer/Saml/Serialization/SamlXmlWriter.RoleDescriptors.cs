// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Metadata;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append the service as a child node
    /// </summary>
    /// <param name="node">parent node</param>
    /// <param name="service">Service that will become child node</param>
    /// <param name="elementName">Name of the element</param>
    protected virtual XmlElement Append(XmlNode node, Endpoint service, string elementName)
    {
        var serviceElement = AppendElement(node, SamlConstants.Namespaces.MetadataPrefix, elementName);

        serviceElement.SetAttribute(SamlConstants.Attributes.Binding, service.Binding);
        serviceElement.SetAttribute(SamlConstants.Attributes.Location, service.Location);

        return serviceElement;
    }
}
