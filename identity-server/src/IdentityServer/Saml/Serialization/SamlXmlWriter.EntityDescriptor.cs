// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <inheritdoc/>
    public virtual XmlDocument Write(EntityDescriptor entityDescriptor)
    {
        var xmlDoc = new XmlDocument();

        Append(xmlDoc, entityDescriptor);

        return xmlDoc;
    }

    /// <summary>
    /// Append the descriptor as a child node
    /// </summary>
    /// <param name="node">parent node</param>
    /// <param name="entityDescriptor">Entity Descriptor</param>
    protected virtual XmlElement Append(XmlNode node, EntityDescriptor entityDescriptor)
    {
        var entityDescriptorElement = AppendElement(node, SamlConstants.Namespaces.MetadataPrefix, SamlConstants.Elements.EntityDescriptor);
        entityDescriptorElement.SetAttribute(SamlConstants.Attributes.ID, entityDescriptor.Id);
        entityDescriptorElement.SetAttribute(SamlConstants.Attributes.entityID, entityDescriptor.EntityId);
        entityDescriptorElement.SetAttributeIfValue(SamlConstants.Attributes.cacheDuration, entityDescriptor.CacheDuration);
        entityDescriptorElement.SetAttributeIfValue(SamlConstants.Attributes.validUntil, entityDescriptor.ValidUntil);

        foreach (var roleDescriptor in entityDescriptor.RoleDescriptors)
        {
            switch (roleDescriptor)
            {
                case IDPSSODescriptor idpSsoDescriptor:
                    Append(entityDescriptorElement, idpSsoDescriptor);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        return entityDescriptorElement;
    }
}
