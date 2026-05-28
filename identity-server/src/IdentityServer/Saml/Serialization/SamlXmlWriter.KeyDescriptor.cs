// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Metadata;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append the key descriptor as a child node
    /// </summary>
    /// <param name="node">parent node</param>
    /// <param name="keyDescriptor">Key descriptor that will become child elements</param>
    protected virtual void Append(XmlNode node, KeyDescriptor keyDescriptor)
    {
        var keyDescriptorElement = AppendElement(node, SamlConstants.Namespaces.MetadataPrefix, SamlConstants.Elements.KeyDescriptor);

        var useValue = keyDescriptor.Use switch
        {
            KeyUse.Signing => "signing",
            KeyUse.Encryption => "encryption",
            _ => null
        };

        if (useValue is not null)
        {
            keyDescriptorElement.SetAttribute(SamlConstants.Attributes.use, useValue);
        }

        if (keyDescriptor.KeyInfo is not null)
        {
            AppendKeyInfo(keyDescriptorElement, keyDescriptor.KeyInfo);
        }
    }
}
