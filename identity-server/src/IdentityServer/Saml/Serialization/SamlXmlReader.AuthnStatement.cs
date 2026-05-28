// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads an AuthnStatement.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>authnStatement read</returns>
    protected AuthnStatement ReadAuthnStatement(XmlTraverser source)
    {
        var authnStatement = Create<AuthnStatement>();

        ReadAttributes(source, authnStatement);
        ReadElements(source.GetChildren(), authnStatement);

        return authnStatement;
    }

    /// <summary>
    /// Reads attributes of an AuthnStatement
    /// </summary>
    /// <param name="source"></param>
    /// <param name="authnStatement"></param>
    protected virtual void ReadAttributes(XmlTraverser source, AuthnStatement authnStatement)
    {
        authnStatement.AuthnInstant = source.GetRequiredDateTimeAttribute(SamlConstants.Attributes.AuthnInstant);
        authnStatement.SessionIndex = source.GetAttribute(SamlConstants.Attributes.SessionIndex);
        authnStatement.SessionNotOnOrAfter = source.GetDateTimeAttribute(SamlConstants.Attributes.SessionNotOnOrAfter);
    }

    /// <summary>
    /// Reads elements of an AuthnStatement.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="authnStatement">AuthnStatement to populate</param>
    protected virtual void ReadElements(XmlTraverser source, AuthnStatement authnStatement)
    {
        source.MoveNext(true);

        if (source.HasName(SamlConstants.Elements.SubjectLocality, SamlConstants.Namespaces.Assertion))
        {
            // We're not supporting Subject Locality.
            source.MoveNext(true);
        }

        if (source.EnsureName(SamlConstants.Elements.AuthnContext, SamlConstants.Namespaces.Assertion))
        {
            authnStatement.AuthnContext = ReadAuthnContext(source);
            source.MoveNext(true);
        }
    }
}
