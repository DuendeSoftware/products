// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Logging;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints;

internal class OAuthMetadataEndpoint(
    IdentityServerOptions options,
    IIssuerPathValidator issuerPathValidator,
    IServerUrls serverUrls,
    IIssuerNameService issuerNameService,
    IDiscoveryResponseGenerator discoveryResponseGenerator,
    SanitizedLogger<OAuthMetadataEndpoint> logger) : BaseDiscoveryEndpoint(options, discoveryResponseGenerator), IEndpointHandler
{
    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity =
            Tracing.BasicActivitySource.StartActivity(
                IdentityServerConstants.EndpointNames.OAuthMetadata + "Endpoint");

        logger.LogTrace("Processing OAuth discovery request.");

        // validate HTTP
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            logger.LogWarning("OAuth Discovery endpoint only supports GET requests");
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        logger.LogDebug("Start OAuth discovery request");

        if (!Options.Endpoints.EnableOAuth2MetadataEndpoint)
        {
            logger.LogInformation("OAuth Discovery endpoint disabled. 404.");
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        if (context.Request.PathBase.HasValue)
        {
            logger.LogDebug("Request for OAuth discovery document contains PathBase. Returning 404");
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        _ = context.Request.Path.StartsWithSegments("/.well-known/oauth-authorization-server", StringComparison.OrdinalIgnoreCase, out var issuerSubPath);
        if (!await issuerPathValidator.ValidateAsync(issuerSubPath))
        {
            logger.LogDebug("Request for OAuth discovery document contains invalid sub-path. Returning 404");
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        if (issuerSubPath.HasValue)
        {
            serverUrls.BasePath = issuerSubPath;
        }

        var issuerUri = await issuerNameService.GetCurrentAsync();
        var baseUrl = serverUrls.BaseUrl;

        if (!issuerUri.Equals($"{context.Request.Scheme}://{context.Request.Host}{issuerSubPath}", StringComparison.Ordinal))
        {
            logger.LogDebug("Request for OAuth discovery document with a request URL that does not match the issuer URI. Returning 404. Issuer: {issuer}, Request: {request}", issuerUri, $"{context.Request.Scheme}://{context.Request.Host}{issuerSubPath}");
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        // generate response
        logger.LogTrace("Calling into discovery response generator: {type}", ResponseGenerator.GetType().FullName);

        return await GetDiscoveryDocument(context, baseUrl, issuerUri);
    }
}
