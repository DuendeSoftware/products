// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <summary>
    /// Read a NameId
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <returns>NameId</returns>
    protected virtual NameId ReadNameId(XmlTraverser source)
    {
        var result = Create<NameId>();

        // Read the text value of the NameID element and the Format attribute
        ReadContents(source, result);
        ReadAttributes(source, result);

        return result;
    }

    /// <summary>
    /// Reads contents of a NameId
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="nameId"></param>
    protected virtual void ReadContents(XmlTraverser source, NameId nameId) => nameId.Value = source.GetTextContents();

    /// <summary>
    /// Reads attributes of a NameId
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="nameId">The NameId to populate</param>
    protected virtual void ReadAttributes(XmlTraverser source, NameId nameId)
    {
        nameId.Format = source.GetAbsoluteUriAttribute(SamlConstants.Attributes.Format);
        nameId.SPNameQualifier = source.GetAttribute(SamlConstants.Attributes.SPNameQualifier);
        nameId.NameQualifier = source.GetAttribute(SamlConstants.Attributes.NameQualifier);
    }
}
