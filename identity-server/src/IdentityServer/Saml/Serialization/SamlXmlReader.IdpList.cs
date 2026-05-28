// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Read IdpList.
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <returns>IdpList</returns>
    protected virtual IdpList ReadIdpList(XmlTraverser source)
    {
        var result = Create<IdpList>();

        ReadElements(source.GetChildren(), result);

        return result;
    }
    /// <summary>
    /// Reads elements of a IdpList.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="result">Subject to populate</param>
    protected virtual void ReadElements(XmlTraverser source, IdpList result)
    {
        // We require at least one element.
        source.MoveNext(false);

        // There should be at least one Idp Entry.
        if (source.EnsureName(SamlConstants.Elements.IDPEntry, SamlConstants.Namespaces.Protocol))
        {
            // Read IdpEntries as long as we find more.
            do
            {
                result.IdpEntries.Add(ReadIdpEntry(source));
            } while (source.MoveNext(true) && source.HasName(SamlConstants.Elements.IDPEntry, SamlConstants.Namespaces.Protocol));
        }

        // Check if source.HasName GetComplete => read it.
        if (source.HasName(SamlConstants.Elements.GetComplete, SamlConstants.Namespaces.Protocol))
        {
            result.GetComplete = source.GetTextContents();
            source.MoveNext(true);
        }
    }
}
