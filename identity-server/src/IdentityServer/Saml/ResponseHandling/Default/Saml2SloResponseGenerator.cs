// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Response generator for SAML 2.0 Single Logout.
/// </summary>
/// <param name="issuerNameService">Issuer name service for Saml2</param>
/// <param name="timeProvider">Clock</param>
/// <param name="samlXmlWriter">XML writer/serializer</param>
/// <param name="samlSigningService">Signing service for SAML responses</param>
public class Saml2SloResponseGenerator(
    ISaml2IssuerNameService issuerNameService,
    TimeProvider timeProvider,
    ISamlXmlWriter samlXmlWriter,
    ISamlSigningService samlSigningService)
    : ISaml2SloResponseGenerator
{
    /// <inheritdoc/>
    public async Task<Saml2FrontChannelResult> CreateSuccessResponse(ValidatedLogoutRequest request, Ct ct)
        => await CreateResponseAsync(request, SamlStatusCodes.Success, subStatusCode: null, statusMessage: null, ct);

    /// <inheritdoc/>
    public async Task<Saml2FrontChannelResult> CreatePartialLogoutResponse(ValidatedLogoutRequest request, Ct ct)
        => await CreateResponseAsync(request, SamlStatusCodes.Success, subStatusCode: SamlStatusCodes.PartialLogout, statusMessage: null, ct);

    /// <summary>
    /// Builds a <see cref="LogoutResponse"/> and wraps it in a <see cref="Saml2FrontChannelResult"/>.
    /// </summary>
    protected virtual async Task<Saml2FrontChannelResult> CreateResponseAsync(
        ValidatedLogoutRequest request,
        string statusCode,
        string? subStatusCode,
        string? statusMessage,
        Ct ct)
    {
        var issuer = await issuerNameService.GetCurrentAsync(ct);
        var destination = request.Saml2Sp?.GetSingleLogoutServiceEndpoint(SamlBinding.HttpRedirect)?.Location
            ?? request.LogoutRequest.Issuer?.Value
            ?? throw new InvalidOperationException(
                "Cannot generate a SAML logout response: no destination could be determined from the service provider's SingleLogoutServiceUrls or the original request's Issuer.");

        var logoutResponse = new LogoutResponse
        {
            Id = XmlHelpers.CreateId(),
            Version = SamlVersions.V2,
            IssueInstant = timeProvider.GetUtcNow().UtcDateTime,
            InResponseTo = request.LogoutRequest.Id,
            Issuer = new NameId(issuer),
            Destination = destination,
            Status = new SamlStatus
            {
                StatusCode = new StatusCode
                {
                    Value = statusCode,
                    NestedStatusCode = subStatusCode != null ? new StatusCode { Value = subStatusCode } : null
                },
                StatusMessage = statusMessage
            }
        };

        var signingCertificate = await samlSigningService.GetSigningCertificateAsync(ct);
        var xml = samlXmlWriter.Write(logoutResponse);

        return new Saml2FrontChannelResult
        {
            Message = new OutboundSaml2Message
            {
                Destination = destination,
                Name = SamlConstants.RequestProperties.SAMLResponse,
                RelayState = request.RelayState ?? request.Saml2Message?.RelayState,
                Xml = xml.DocumentElement!,
                SigningCertificate = signingCertificate,
                Binding = request.Binding
            }
        };
    }
}
