// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Metadata;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append the descriptor as a child node
    /// </summary>
    /// <param name="node">parent node</param>
    /// <param name="idpSsoDescriptor">IDPSSO Descriptor</param>
    protected virtual XmlElement Append(XmlNode node, IDPSSODescriptor idpSsoDescriptor)
    {
        var idpSsoDescriptorElement = AppendElement(node, SamlConstants.Namespaces.MetadataPrefix, SamlConstants.Elements.IDPSSODescriptor);
        idpSsoDescriptorElement.SetAttribute(SamlConstants.Attributes.protocolSupportEnumeration, idpSsoDescriptor.ProtocolSupportEnumeration);

        if (idpSsoDescriptor.WantAuthnRequestsSigned == true)
        {
            idpSsoDescriptorElement.SetAttribute(SamlConstants.Attributes.WantAuthnRequestsSigned, "true");
        }

        foreach (var key in idpSsoDescriptor.Keys)
        {
            Append(idpSsoDescriptorElement, key);
        }

        foreach (var singleLogoutService in idpSsoDescriptor.SingleLogoutServices)
        {
            Append(idpSsoDescriptorElement, singleLogoutService, SamlConstants.Elements.SingleLogoutService);
        }

        foreach (var nameIdFormat in idpSsoDescriptor.NameIdFormats)
        {
            var nameIdFormatElement = AppendElement(idpSsoDescriptorElement, SamlConstants.Namespaces.MetadataPrefix, SamlConstants.Elements.NameIDFormat);
            nameIdFormatElement.InnerText = nameIdFormat;
        }

        foreach (var singleSignOnService in idpSsoDescriptor.SingleSignOnServices)
        {
            Append(idpSsoDescriptorElement, singleSignOnService, SamlConstants.Elements.SingleSignOnService);
        }

        return idpSsoDescriptorElement;
    }
}
