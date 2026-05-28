// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader

{
    /// <summary>
    /// Reads Status
    /// </summary>
    /// <param name="source">Xml Traverser</param>
    /// <returns>Status</returns>
    protected virtual SamlStatus ReadStatus(XmlTraverser source)
    {
        var result = Create<SamlStatus>();

        ReadElements(source.GetChildren(), result);

        return result;
    }

    /// <summary>
    /// Reads elements of SamlStatus
    /// </summary>
    /// <param name="source">Xml Traverser</param>
    /// <param name="status">Status to populate</param>
    protected virtual void ReadElements(XmlTraverser source, SamlStatus status)
    {
        source.MoveNext();

        if (source.EnsureName(SamlConstants.Elements.StatusCode, SamlConstants.Namespaces.Protocol))
        {
            status.StatusCode = ReadStatusCode(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.StatusMessage, SamlConstants.Namespaces.Protocol))
        {
            status.StatusMessage = source.GetTextContents();
            source.MoveNext(true);
        }
    }

    /// <summary>
    /// Reads a status code
    /// </summary>
    /// <param name="source"></param>
    protected virtual StatusCode ReadStatusCode(XmlTraverser source)
    {
        var result = Create<StatusCode>();

        ReadAttributes(source, result);

        var children = source.GetChildren();
        if (children.MoveNext(true) && children.HasName(SamlConstants.Elements.StatusCode, SamlConstants.Namespaces.Protocol))
        {
            result.NestedStatusCode = ReadStatusCode(children);
            children.MoveNext(true);
        }

        return result;
    }

    /// <summary>
    /// Reads attributes of StatusCode
    /// </summary>
    /// <param name="source"></param>
    /// <param name="statusCode"></param>
    protected virtual void ReadAttributes(XmlTraverser source, StatusCode statusCode) =>
        statusCode.Value = source.GetRequiredAbsoluteUriAttribute(SamlConstants.Attributes.Value)!;
}
