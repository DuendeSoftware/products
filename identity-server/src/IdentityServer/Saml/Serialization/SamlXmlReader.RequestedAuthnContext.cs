// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads a RequestedAuthnContext.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>read</returns>
    protected RequestedAuthnContext ReadRequestedAuthnContext(XmlTraverser source)
    {
        var result = Create<RequestedAuthnContext>();

        ReadElements(source.GetChildren(), result);
        ReadAttributes(source, result);

        return result;
    }

    /// <summary>
    /// Reads elements of a requestedAuthnContext.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="requestedAuthnContext">RequestedAuthnContext to populate</param>
    protected virtual void ReadElements(XmlTraverser source, RequestedAuthnContext requestedAuthnContext)
    {
        // We require at least one element.
        source.MoveNext(false);

        XmlNode lastNode = null!;
        do
        {
            lastNode = source.CurrentNode;
            if (source.HasName(SamlConstants.Elements.AuthnContextClassRef, SamlConstants.Namespaces.Assertion))
            {
                requestedAuthnContext.AuthnContextClassRef.Add(source.GetTextContents());
            }
            else if (source.HasName(SamlConstants.Elements.AuthnContextDeclRef, SamlConstants.Namespaces.Assertion))
            {
                requestedAuthnContext.AuthnContextDeclRef.Add(source.GetTextContents());
            }
        } while (source.MoveNext(true)); // Read all elements found.

        if (requestedAuthnContext.AuthnContextClassRef.Count > 0 && requestedAuthnContext.AuthnContextDeclRef.Count > 0)
        {
            source.Errors.Add(new(ErrorReason.InvalidElementCombination, SamlConstants.Elements.RequestedAuthnContext,
                lastNode,
                "RequestedAuthnContext must contain either AuthnContextClassRef or AuthnContextDeclRef elements, but not both"));
        }
    }

    /// <summary>
    /// Read RequestedAuthnContext attributes.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="requestedAuthnContext">RequestedAuthnContext</param>
    protected virtual void ReadAttributes(XmlTraverser source, RequestedAuthnContext requestedAuthnContext) =>
        requestedAuthnContext.Comparison = source.GetAttribute(SamlConstants.Attributes.Comparison) ?? "";
}
