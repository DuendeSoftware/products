// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads an endpoint.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>Endpoint read</returns>
    protected Endpoint ReadEndpoint(XmlTraverser source)
    {
        var result = Create<Endpoint>();
        ReadAttributes(source, result);

        return result;
    }

    /// <summary>
    /// Read endpoint attributes.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="endpoint">Endpoint</param>
    protected virtual void ReadAttributes(XmlTraverser source, Endpoint endpoint)
    {
        endpoint.Binding = source.GetRequiredAbsoluteUriAttribute(SamlConstants.Attributes.Binding) ?? "";
        endpoint.Location = source.GetRequiredAbsoluteUriAttribute(SamlConstants.Attributes.Location) ?? "";
    }

    /// <summary>
    /// Read indexed endpoint
    /// </summary>
    /// <param name="source">Source</param>
    /// <returns>IndexedEndpoint</returns>
    protected virtual IndexedEndpoint ReadIndexedEndpoint(XmlTraverser source)
    {
        var result = Create<IndexedEndpoint>();
        result.Index = source.GetRequiredIntAttribute(SamlConstants.Attributes.index);
        result.IsDefault = source.GetBoolAttribute(SamlConstants.Attributes.isDefault) ?? false;

        ReadAttributes(source, result);

        return result;
    }
}
