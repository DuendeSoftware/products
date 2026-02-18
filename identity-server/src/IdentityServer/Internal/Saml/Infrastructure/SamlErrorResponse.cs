
// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Represents a SAML error response that will be sent to the Service Provider.
/// </summary>
internal class SamlErrorResponse : EndpointResult<SamlErrorResponse>
{
    /// <summary>
    /// Gets the SAML binding to use for sending the response (HTTP-POST or HTTP-Redirect).
    /// </summary>
    public required SamlBinding Binding { get; init; }

    /// <summary>
    /// Gets the SAML status code for the error.
    /// </summary>
    public required SamlStatusCode StatusCode { get; init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the Assertion Consumer Service URL to send the response to.
    /// </summary>
    public required Uri AssertionConsumerServiceUrl { get; init; }

    /// <summary>
    /// Gets the IdP issuer URI.
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets the request ID this response is replying to (InResponseTo), or null for IdP-initiated.
    /// </summary>
    public string? InResponseTo { get; init; }

    /// <summary>
    /// Gets the RelayState to preserve across the response.
    /// </summary>
    public string? RelayState { get; init; }

    /// <summary>
    /// Gets an optional secondary status code for more specific error information.
    /// </summary>
    public SamlStatusCode? SubStatusCode { get; init; }

    /// <summary>
    /// Gets or sets the Service Provider where the response will be sent.
    /// </summary>
    public required SamlServiceProvider ServiceProvider { get; init; }

    internal class ResponseWriter(ISamlResultSerializer<SamlErrorResponse> serializer)
        : IHttpResponseWriter<SamlErrorResponse>
    {
        public async Task WriteHttpResponse(SamlErrorResponse result, HttpContext httpContext)
        {
            var responseElement = serializer.Serialize(result);

            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), responseElement);
            await using var stringWriter = new StringWriter();
            await using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Encoding = Encoding.UTF8,
                Indent = false,
                Async = true
            }))
            {
                doc.Save(xmlWriter);
                await xmlWriter.FlushAsync();
            }

            var encodedResponse = Convert.ToBase64String(Encoding.UTF8.GetBytes(stringWriter.ToString()));

            // Generate HTML form that auto-submits to the ACS URL
            var html = HttpResponseBindings.GenerateAutoPostForm(SamlMessageName.SamlResponse, encodedResponse, result.AssertionConsumerServiceUrl,
                result.RelayState);

            httpContext.Response.ContentType = "text/html";
            httpContext.Response.Headers.CacheControl = "no-cache, no-store";
            httpContext.Response.Headers.Pragma = "no-cache";

            await httpContext.Response.WriteAsync(html);
        }
    }
}
