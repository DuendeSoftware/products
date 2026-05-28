// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <summary>
    /// Read attributes of <see cref="RequestAbstractType"/>
    /// </summary>
    /// <param name="source">Xml travers</param>
    /// <param name="request">RequestAbstractType</param>
    protected virtual void ReadAttributes(XmlTraverser source, RequestAbstractType request)
    {
        request.Id = source.GetRequiredAttribute(SamlConstants.Attributes.ID);
        request.IssueInstant = source.GetRequiredDateTimeAttribute(SamlConstants.Attributes.IssueInstant);
        request.Version = source.GetRequiredAttribute(SamlConstants.Attributes.Version);
        request.Destination = source.GetAttribute(SamlConstants.Attributes.Destination);
        request.Consent = source.GetAttribute(SamlConstants.Attributes.Consent);
    }

    /// <summary>
    /// Reads the child elements of a RequestAbstractType
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="request">RequestAbstractType to populate</param>
    /// <param name="ct">Cancellation token</param>
    protected virtual async Task ReadElementsAsync(XmlTraverser source, RequestAbstractType request, Ct ct)
    {
        source.MoveNext(true);

        if (source.HasName(SamlConstants.Elements.Issuer, SamlConstants.Namespaces.Assertion))
        {
            request.Issuer = ReadNameId(source);
            source.MoveNext(true);
        }

        (var trustedSigningKeys, var allowedHashAlgorithms) =
            await GetSignatureValidationParametersFromIssuerAsync(source, request.Issuer, ct);

        if (source.ReadAndValidateOptionalSignature(trustedSigningKeys, allowedHashAlgorithms))
        {
            source.MoveNext(true);
        }

        request.TrustLevel = source.TrustLevel;

        if (source.HasName(SamlConstants.Elements.Extensions, SamlConstants.Namespaces.Protocol))
        {
            request.Extensions = ReadExtensions(source);
            source.MoveNext(true);
        }
    }
}
