// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Read the current node as an IDPSSODescriptor
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual IDPSSODescriptor ReadIDPSSODescriptor(XmlTraverser source)
    {
        var result = Create<IDPSSODescriptor>();

        ReadAttributes(source, result);
        ReadElements(source.GetChildren(), result);

        return result;
    }

    /// <summary>
    /// Read attributes of IDPSSODescriptor.
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="result">Result</param>
    protected virtual void ReadAttributes(XmlTraverser source, IDPSSODescriptor result)
    {
        ReadAttributes(source, (SSODescriptor)result);

        result.WantAuthnRequestsSigned = source.GetBoolAttribute(SamlConstants.Attributes.WantAuthnRequestsSigned);
    }

    /// <summary>
    /// Read child elements of IDPSSODescriptor
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="result"></param>
    protected virtual void ReadElements(XmlTraverser source, IDPSSODescriptor result)
    {
        ReadElements(source, (SSODescriptor)result);

        // We must have at least one SingleSignOnService in an IDPSSODescriptor and now we should be at it.
        if (!source.EnsureName(SamlConstants.Elements.SingleSignOnService, SamlConstants.Namespaces.Metadata))
        {
            return;
        }

        do
        {
            result.SingleSignOnServices.Add(ReadEndpoint(source));
        } while (source.MoveNext(true) && source.HasName(SamlConstants.Elements.SingleSignOnService, SamlConstants.Namespaces.Metadata));

        source.Skip();
    }
}
