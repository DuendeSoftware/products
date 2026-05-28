// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.Xml;
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Process a RoleDescriptor element.
    /// </summary>
    /// <param name="source">Source</param>
    /// <returns>True if current node was a RoleDescriptor element</returns>
    protected virtual RoleDescriptor ReadRoleDescriptor(XmlTraverser source)
    {
        var result = Create<RoleDescriptor>();

        ReadAttributes(source, result);
        ReadElements(source.GetChildren(), result);

        // Custom RoleDesciptors might have other elements that we do not know - ignore them.
        source.IgnoreChildren();

        return result;
    }

    /// <summary>
    /// Read attributs of RoleDescriptor
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="result">Target to set properties on</param>
    protected virtual void ReadAttributes(XmlTraverser source, RoleDescriptor result) =>
        result.ProtocolSupportEnumeration =
            source.GetRequiredAbsoluteUriAttribute(SamlConstants.Attributes.protocolSupportEnumeration);

    /// <summary>
    /// Read elements of RoleDescriptor
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="result">Target to set properties on</param>
    /// <returns>More elements available?</returns>
    protected virtual void ReadElements(XmlTraverser source, RoleDescriptor result)
    {
        source.MoveNext(true);

        if (source.HasName(SamlConstants.Elements.Signature, SignedXml.XmlDsigNamespaceUrl))
        {
            // Signatures on RoleDescriptors are not supported.
            source.IgnoreChildren();

            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.Extensions, SamlConstants.Namespaces.Metadata))
        {
            // Extensions on RoleDescriptors are not supported.
            source.IgnoreChildren();

            source.MoveNext(true);
        }

        while (source.HasName(SamlConstants.Elements.KeyDescriptor, SamlConstants.Namespaces.Metadata))
        {
            result.Keys.Add(ReadKeyDescriptor(source));
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.Organization, SamlConstants.Namespaces.Metadata))
        {
            // Organization reading is not supported.
            source.IgnoreChildren();

            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.ContactPerson, SamlConstants.Namespaces.Metadata))
        {
            // Contact person reading is not supported.
            source.IgnoreChildren();

            source.MoveNext(true);
        }
    }
}
