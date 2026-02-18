// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using LogoutRequest = Duende.IdentityServer.Internal.Saml.SingleLogout.Models.LogoutRequest;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal class SamlFrontChannelLogoutRequestBuilder(
    TimeProvider timeProvider,
    SamlProtocolMessageSigner samlProtocolMessageSigner)
{
    internal async Task<ISamlFrontChannelLogout> BuildLogoutRequestAsync(
        SamlServiceProvider serviceProvider,
        string nameId,
        string? nameIdFormat,
        string sessionIndex,
        string issuer)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (serviceProvider.SingleLogoutServiceUrl == null)
        {
            throw new InvalidOperationException(
                $"Service Provider '{serviceProvider.EntityId}' has no SingleLogoutServiceUrl configured");
        }

        var logoutRequest = new LogoutRequest
        {
            Id = RequestId.New(),
            Version = SamlVersion.V2,
            IssueInstant = timeProvider.GetUtcNow().UtcDateTime,
            Destination = serviceProvider.SingleLogoutServiceUrl.Location,
            Issuer = issuer,
            NameId = new NameIdentifier { Value = nameId, Format = nameIdFormat },
            SessionIndex = sessionIndex
        };

        var requestXml = SerializeLogoutRequest(logoutRequest);

        return serviceProvider.SingleLogoutServiceUrl.Binding switch
        {
            SamlBinding.HttpRedirect => await BuildRedirectLogoutRequest(serviceProvider.SingleLogoutServiceUrl.Location, requestXml),
            SamlBinding.HttpPost => await BuildHttpPostLogoutRequest(serviceProvider, requestXml),
            _ => throw new InvalidOperationException(
                $"Binding '{serviceProvider.SingleLogoutServiceUrl.Binding}' is not supported")
        };
    }

    private static XElement SerializeLogoutRequest(LogoutRequest logoutRequest)
    {
        var issueInstant =
            logoutRequest.IssueInstant.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var protocolNs = XNamespace.Get(SamlConstants.Namespaces.Protocol);
        var assertionNs = XNamespace.Get(SamlConstants.Namespaces.Assertion);

        var requestElement = new XElement(protocolNs + LogoutRequest.ElementNames.RootElement,
            new XAttribute("ID", logoutRequest.Id.Value),
            new XAttribute("Version", logoutRequest.Version.ToString()),
            new XAttribute("IssueInstant", issueInstant),
            new XAttribute("Destination", logoutRequest.Destination!),
            new XElement(assertionNs + LogoutRequest.ElementNames.Issuer, logoutRequest.Issuer));

        var nameIdElement = new XElement(assertionNs + LogoutRequest.ElementNames.NameID, logoutRequest.NameId.Value);
        if (!string.IsNullOrEmpty(logoutRequest.NameId.Format))
        {
            nameIdElement.Add(new XAttribute("Format", logoutRequest.NameId.Format));
        }

        requestElement.Add(nameIdElement);

        requestElement.Add(new XElement(protocolNs + LogoutRequest.ElementNames.SessionIndex,
            logoutRequest.SessionIndex));

        if (logoutRequest.Reason.HasValue)
        {
            var reasonValue = logoutRequest.Reason.Value switch
            {
                LogoutReason.User => "urn:oasis:names:tc:SAML:2.0:logout:user",
                LogoutReason.Admin => "urn:oasis:names:tc:SAML:2.0:logout:admin",
                LogoutReason.GlobalTimeout => "urn:oasis:names:tc:SAML:2.0:logout:global-timeout",
                _ => null
            };

            if (reasonValue != null)
            {
                requestElement.Add(new XAttribute("Reason", reasonValue));
            }
        }

        if (logoutRequest.NotOnOrAfter.HasValue)
        {
            var notOnOrAfter =
                logoutRequest.NotOnOrAfter.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            requestElement.Add(new XAttribute("NotOnOrAfter", notOnOrAfter));
        }

        return requestElement;
    }

    private async Task<ISamlFrontChannelLogout> BuildRedirectLogoutRequest(Uri singleLogoutServiceUri, XElement requestXml)
    {
        var encodedRequest = DeflateAndEncode(requestXml.ToString());

        var queryString = $"?SAMLRequest={Uri.EscapeDataString(encodedRequest)}";

        var signedQueryString = await samlProtocolMessageSigner.SignQueryString(queryString);

        return new SamlHttpRedirectFrontChannelLogout(singleLogoutServiceUri, signedQueryString);
    }

    private static string DeflateAndEncode(string xml)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);

        using var output = new MemoryStream();
        using (var deflateStream = new DeflateStream(output, CompressionLevel.Optimal))
        {
            deflateStream.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    private async Task<ISamlFrontChannelLogout> BuildHttpPostLogoutRequest(SamlServiceProvider serviceProvider, XElement requestXml)
    {
        var signedRequestXml = await samlProtocolMessageSigner.SignProtocolMessage(requestXml, serviceProvider);

        var encodedXml = Convert.ToBase64String(Encoding.UTF8.GetBytes(signedRequestXml));

        return new SamlHttpPostFrontChannelLogout(serviceProvider.SingleLogoutServiceUrl!.Location, encodedXml, null);
    }
}
