// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout.Models;

/// <summary>
/// Represents a SAML logout request with binding information.
/// </summary>
internal record SamlLogoutRequest : SamlRequestBase<LogoutRequest>
{
    public static async ValueTask<SamlLogoutRequest?> BindAsync(HttpContext context)
    {
        var extractor = context.RequestServices.GetRequiredService<SamlLogoutRequestExtractor>();
        return await extractor.ExtractAsync(context);
    }

    public LogoutRequest LogoutRequest => Request;
}

internal class SamlLogoutRequestExtractor : SamlRequestExtractor<LogoutRequest, SamlLogoutRequest>
{
    private readonly LogoutRequestParser _parser;

    public SamlLogoutRequestExtractor(LogoutRequestParser parser) => _parser = parser;

    protected override LogoutRequest ParseRequest(XDocument xmlDocument) => _parser.Parse(xmlDocument);

    protected override SamlLogoutRequest CreateResult(
        LogoutRequest parsedRequest,
        XDocument requestXml,
        SamlBinding binding,
        string? relayState,
        string? signature = null,
        string? signatureAlgorithm = null,
        string? encodedSamlRequest = null) => new SamlLogoutRequest
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
