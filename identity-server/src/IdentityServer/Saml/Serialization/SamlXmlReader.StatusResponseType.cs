// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Read attributes of a status response
    /// </summary>
    /// <param name="source">Xml traverser</param>
    /// <param name="response">StatusResponse</param>
    protected virtual void ReadAttributes(XmlTraverser source, StatusResponseType response)
    {
        response.Id = source.GetRequiredAttribute(SamlConstants.Attributes.ID);
        response.Version = source.GetRequiredAttribute(SamlConstants.Attributes.Version);
        response.IssueInstant = source.GetRequiredDateTimeAttribute(SamlConstants.Attributes.IssueInstant);
        response.InResponseTo = source.GetAttribute(SamlConstants.Attributes.InResponseTo);
        response.Destination = source.GetAttribute(SamlConstants.Attributes.Destination);
    }

    /// <summary>
    /// Read elements of abstract StatusResponseType
    /// </summary>
    /// <param name="source">XML Traverser</param>
    /// <param name="response">Response to populate</param>
    /// <param name="ct">Cancellation token</param>
    protected virtual async Task ReadElementsAsync(XmlTraverser source, StatusResponseType response, Ct ct)
    {
        source.MoveNext();

        if (source.HasName(SamlConstants.Elements.Issuer, SamlConstants.Namespaces.Assertion))
        {
            response.Issuer = ReadNameId(source);

            source.MoveNext();
        }

        (var trustedSigningKeys, var allowedHashAlgorithms) =
             await GetSignatureValidationParametersFromIssuerAsync(source, response.Issuer, ct);

        if (source.ReadAndValidateOptionalSignature(trustedSigningKeys, allowedHashAlgorithms))
        {
            response.TrustLevel = source.TrustLevel;
            source.MoveNext();
        }

        if (source.HasName(SamlConstants.Elements.Extensions, SamlConstants.Namespaces.Protocol))
        {
            response.Extensions = ReadExtensions(source);
            source.MoveNext();
        }

        // Status is optional on XML schema level, but Core 2.3.3. says that
        // "an assertion without a subject has no defined meaning in this specification."
        // so we are treating it as mandatory.
        if (source.EnsureName(SamlConstants.Elements.Status, SamlConstants.Namespaces.Protocol))
        {
            response.Status = ReadStatus(source);
            source.MoveNext(true);
        }
    }
}
