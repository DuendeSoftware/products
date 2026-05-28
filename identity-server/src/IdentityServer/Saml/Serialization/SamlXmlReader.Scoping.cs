// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads a Scoping.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>read</returns>
    protected Scoping ReadScoping(XmlTraverser source)
    {
        var result = Create<Scoping>();

        ReadElements(source.GetChildren(), result);
        ReadAttributes(source, result);

        return result;
    }

    /// <summary>
    /// Reads elements of a Scoping.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="scoping">Scoping</param>
    protected virtual void ReadElements(XmlTraverser source, Scoping scoping)
    {
        // We require at least one element.
        if (!source.MoveNext(false))
        {
            return;
        }

        do
        {
            if (source.HasName(SamlConstants.Elements.IDPList, SamlConstants.Namespaces.Protocol))
            {
                scoping.IDPList = ReadIdpList(source);
            }
            else
            {
                if (source.HasName(SamlConstants.Elements.RequesterID, SamlConstants.Namespaces.Protocol))
                {
                    scoping.RequesterID.Add(source.GetAbsoluteUriContents());
                }
                else
                {
                    source.Errors.Add(new(
                        ErrorReason.ExtraElements,
                        source.CurrentNode!.LocalName,
                        source.CurrentNode,
                        $"Unexpected element \"{source.CurrentNode.LocalName}\" in Scoping."));
                }
            }
        } while (source.MoveNext(true));
    }

    /// <summary>
    /// Read Scoping attributes.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="scoping">Scoping</param>
    protected virtual void ReadAttributes(XmlTraverser source, Scoping scoping) =>
        scoping.ProxyCount = source.GetIntAttribute(SamlConstants.Attributes.ProxyCount);
}
