// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Saml.Endpoints.Results;

/// <summary>
/// Result of Saml2 Metadata generation
/// </summary>
public sealed class Saml2MetadataResult : EndpointResult<Saml2MetadataResult>
{
    /// <summary>
    /// The metadata as an XML document
    /// </summary>
    public required XmlDocument Xml { get; set; }
}

/// <summary>
/// Write a Saml2 Metadata document to the HttpContext
/// </summary>
public sealed class Saml2MetadataResultWriter : IHttpResponseWriter<Saml2MetadataResult>
{
    /// <inheritdoc/>
    public async Task WriteHttpResponse(Saml2MetadataResult result, HttpContext context)
    {
        // The content-type for the metadata MUST be application/samlmetadata+xml
        // but the requirement is rarely honored by implementations, and it
        // makes the browser download the file instead of displaying. A middle ground
        // is to check if accept header indicates it's a browser and then give it XML,
        // and if not respond with the correct content type.
        context.Response.ContentType =
            context.Request.Headers.Accept.FirstOrDefault()?.Contains("text/html", StringComparison.Ordinal) ?? false
                ? "text/xml"
                : SamlConstants.ContentTypes.Metadata;

        await context.Response.WriteAsync(result.Xml.OuterXml);
    }
}
