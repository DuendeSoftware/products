// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;
using SamlLogoutRequest = Duende.IdentityServer.Saml.Samlp.LogoutRequest;

namespace Duende.IdentityServer.Saml.Services;

/// <summary>
/// Builds outbound SAML 2.0 LogoutRequest messages for front-channel logout notifications.
/// </summary>
public sealed class Saml2FrontChannelLogoutRequestBuilder(
    TimeProvider timeProvider,
    ISamlXmlWriter samlXmlWriter,
    ISamlSigningService samlSigningService) : ISaml2FrontChannelLogoutRequestBuilder
{
    /// <inheritdoc/>
    public async Task<SamlLogoutRequestContext> BuildLogoutRequestAsync(
        SamlServiceProvider serviceProvider,
        string nameId,
        string? nameIdFormat,
        string sessionIndex,
        string issuer,
        Ct ct)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (serviceProvider.SingleLogoutServiceUrls.Count == 0)
        {
            throw new InvalidOperationException(
                $"Service Provider '{serviceProvider.EntityId}' has no SingleLogoutServiceUrls configured");
        }

        var sloEndpoint = serviceProvider.GetSingleLogoutServiceEndpoint(SamlBinding.HttpRedirect);
        if (sloEndpoint == null)
        {
            throw new InvalidOperationException(
                $"Service Provider '{serviceProvider.EntityId}' has no endpoint with HTTP-Redirect binding. " +
                "Only HTTP-Redirect is supported for front-channel logout notifications");
        }

        var destination = sloEndpoint.Location
            ?? throw new InvalidOperationException(
                $"Service Provider '{serviceProvider.EntityId}' SingleLogoutServiceUrl has no Location");

        var logoutRequest = new SamlLogoutRequest
        {
            Id = XmlHelpers.CreateId(),
            Version = Models.SamlVersions.V2,
            IssueInstant = timeProvider.GetUtcNow().UtcDateTime,
            Destination = destination,
            Issuer = new NameId(issuer),
            NameId = new NameId(nameId) { Format = nameIdFormat },
            SessionIndex = sessionIndex
        };

        var xmlDoc = samlXmlWriter.Write(logoutRequest);
        var signingCertificate = await samlSigningService.GetSigningCertificateAsync(ct);

        var message = new OutboundSaml2Message
        {
            Name = SamlConstants.RequestProperties.SAMLRequest,
            Xml = xmlDoc.DocumentElement!,
            Destination = destination,
            Binding = SamlConstants.Bindings.HttpRedirect,
            SigningCertificate = signingCertificate
        };

        return new SamlLogoutRequestContext(message, logoutRequest.Id, serviceProvider.EntityId);
    }
}
