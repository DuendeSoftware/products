// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads an AuthnContext.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>authnContext read</returns>
    protected AuthnContext ReadAuthnContext(XmlTraverser source)
    {
        var authnContext = Create<AuthnContext>();

        ReadElements(source.GetChildren(), authnContext);

        return authnContext;
    }

    /// <summary>
    /// Reads elements of an AuthnContext.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="authnContext">AuthnContext to populate</param>
    protected virtual void ReadElements(XmlTraverser source, AuthnContext authnContext)
    {
        source.MoveNext(true);

        if (source.HasName(SamlConstants.Elements.AuthnContextClassRef, SamlConstants.Namespaces.Assertion))
        {
            authnContext.AuthnContextClassRef = source.GetAbsoluteUriContents();
            source.MoveNext(true);
        }

        // We only support AuthnContextClassRef so far
        source.Skip();
    }
}
