// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout.Models;

/// <summary>
/// Represents a SAML 2.0 LogoutResponse message.
/// </summary>
internal class LogoutResponse : EndpointResult<LogoutResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier for this response.
    /// </summary>
    public required ResponseId Id { get; set; }

    /// <summary>
    /// Gets or sets the SAML version. Must be "2.0".
    /// </summary>
    public SamlVersion Version { get; set; } = SamlVersion.V2;

    /// <summary>
    /// Gets or sets the time instant of issue in UTC.
    /// </summary>
    public required DateTime IssueInstant { get; set; }

    /// <summary>
    /// Gets or sets the URI of the destination endpoint where this response is sent.
    /// </summary>
    public required Uri Destination { get; set; }

    /// <summary>
    /// Gets or sets the entity identifier of the issuer (sender) of this response.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the ID of the LogoutRequest to which this is a response.
    /// </summary>
    public required string InResponseTo { get; set; }

    /// <summary>
    /// Gets or sets the status of the logout operation.
    /// </summary>
    public required Status Status { get; set; }

    /// <summary>
    /// Gets or sets the service provider configuration for this response.
    /// </summary>
    public required SamlServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// Gets or sets the optional RelayState parameter to return to the SP.
    /// </summary>
    public string? RelayState { get; set; }

    internal static class ElementNames
    {
        public const string RootElement = "LogoutResponse";
    }

    internal class ResponseWriter(ISamlResultSerializer<LogoutResponse> serializer, SamlProtocolMessageSigner samlProtocolMessageSigner) : IHttpResponseWriter<LogoutResponse>
    {
        public async Task WriteHttpResponse(LogoutResponse result, HttpContext httpContext)
        {
            var responseXml = serializer.Serialize(result);

            var signedResponseXml = await samlProtocolMessageSigner.SignProtocolMessage(responseXml, result.ServiceProvider);

            var encodedResponse = Convert.ToBase64String(Encoding.UTF8.GetBytes(signedResponseXml));

            var html = HttpResponseBindings.GenerateAutoPostForm(SamlMessageName.SamlResponse, encodedResponse, result.Destination, result.RelayState);

            httpContext.Response.ContentType = "text/html";
            httpContext.Response.Headers.CacheControl = "no-cache, no-store";
            httpContext.Response.Headers.Pragma = "no-cache";

            await httpContext.Response.WriteAsync(html);
        }
    }

    internal class Serializer : ISamlResultSerializer<LogoutResponse>
    {
        public XElement Serialize(LogoutResponse toSerialize)
        {
            var issueInstant = toSerialize.IssueInstant.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            var protocolNs = XNamespace.Get(SamlConstants.Namespaces.Protocol);

            // Build Status element
            var statusCodeElement = new XElement(protocolNs + "StatusCode",
                new XAttribute("Value", toSerialize.Status.StatusCode.ToString()));

            if (!string.IsNullOrEmpty(toSerialize.Status.NestedStatusCode))
            {
                statusCodeElement.Add(
                    new XElement(protocolNs + "StatusCode",
                        new XAttribute("Value", toSerialize.Status.NestedStatusCode)));
            }

            var statusElement = new XElement(protocolNs + "Status", statusCodeElement);

            if (!string.IsNullOrEmpty(toSerialize.Status.StatusMessage))
            {
                statusElement.Add(new XElement(protocolNs + "StatusMessage", toSerialize.Status.StatusMessage));
            }

            // Build LogoutResponse element
            var responseElement = new XElement(protocolNs + ElementNames.RootElement,
                new XAttribute("ID", toSerialize.Id.Value),
                new XAttribute("Version", toSerialize.Version.ToString()),
                new XAttribute("IssueInstant", issueInstant),
                new XAttribute("Destination", toSerialize.Destination),
                new XAttribute("InResponseTo", toSerialize.InResponseTo),
                new XElement(XNamespace.Get(SamlConstants.Namespaces.Assertion) + "Issuer", toSerialize.Issuer),
                statusElement);

            return responseElement;
        }
    }
}
