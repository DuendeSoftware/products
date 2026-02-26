// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents a saml signin request, either as a Redirect Binding or a Post Binding.
/// </summary>
internal record SamlSigninRequest : SamlRequestBase<AuthNRequest>
{
    public static async ValueTask<SamlSigninRequest?> BindAsync(HttpContext context)
    {
        var extractor = context.RequestServices.GetRequiredService<SamlSigninRequestExtractor>();
        return await extractor.ExtractAsync(context);
    }

    public AuthNRequest AuthNRequest => Request;
}

internal class SamlSigninRequestExtractor(AuthNRequestParser parser)
    : SamlRequestExtractor<AuthNRequest, SamlSigninRequest>
{
    protected override AuthNRequest ParseRequest(XDocument xmlDocument) => parser.Parse(xmlDocument);

    protected override SamlSigninRequest CreateResult(
        AuthNRequest parsedRequest,
        XDocument requestXml,
        SamlBinding binding,
        string? relayState,
        string? signature = null,
        string? signatureAlgorithm = null,
        string? encodedSamlRequest = null) => new()
        {
            Request = parsedRequest,
            RequestXml = requestXml,
            Binding = binding,
            RelayState = relayState,
            Signature = signature,
            SignatureAlgorithm = signatureAlgorithm,
            EncodedSamlRequest = encodedSamlRequest
        };
}
