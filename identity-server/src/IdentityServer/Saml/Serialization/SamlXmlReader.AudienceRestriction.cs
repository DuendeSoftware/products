// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <summary>
    /// Reads an AudienceRestriction
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>AudienceRestriction read</returns>
    protected AudienceRestriction ReadAudienceRestriction(XmlTraverser source)
    {
        var result = Create<AudienceRestriction>();

        ReadElements(source.GetChildren(), result);

        return result;
    }

    /// <summary>
    /// Read elements of AudienceRestriction
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="result">AudienceRestriction to populate</param>
    protected virtual void ReadElements(XmlTraverser source, AudienceRestriction result)
    {
        source.MoveNext();

        while (source.EnsureName(SamlConstants.Elements.Audience, SamlConstants.Namespaces.Assertion))
        {
            result.Audiences.Add(source.GetTextContents());
            source.MoveNext(true);
        }
    }
}
