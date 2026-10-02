// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints;

internal class OAuthMetadataEndpoint(
    IdentityServerOptions options,
    IIssuerPathValidator issuerPathValidator,
    IServerUrls serverUrls,
    IIssuerNameService issuerNameService,
    IDiscoveryResponseGenerator discoveryResponseGenerator,
    ILogger<OAuthMetadataEndpoint> logger) : BaseDiscoveryEndpoint(options, discoveryResponseGenerator), IEndpointHandler
{
    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity =
            Tracing.BasicActivitySource.StartActivity(
                IdentityServerConstants.EndpointNames.OAuthMetadata + "Endpoint");

        logger.ProcessingOAuthDiscoveryRequest();

        // validate HTTP
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            logger.OAuthDiscoveryEndpointOnlySupportsGETRequests();
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        logger.StartOAuthDiscoveryRequest();

        if (!Options.Endpoints.EnableOAuth2MetadataEndpoint)
        {
            logger.OAuthDiscoveryEndpointDisabled404();
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        if (context.Request.PathBase.HasValue)
        {
            logger.RequestForOAuthDiscoveryDocumentContainsPathBaseReturning();
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        context.Request.Path.StartsWithSegments("/.well-known/oauth-authorization-server", StringComparison.OrdinalIgnoreCase, out var issuerSubPath);
        if (!await issuerPathValidator.ValidateAsync(issuerSubPath, context.RequestAborted))
        {
            logger.RequestForOAuthDiscoveryDocumentContainsInvalidSub();
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        if (issuerSubPath.HasValue)
        {
            serverUrls.BasePath = issuerSubPath;
        }

        var issuerUri = await issuerNameService.GetCurrentAsync(context.RequestAborted);
        var baseUrl = serverUrls.BaseUrl;

        if (!issuerUri.Equals($"{context.Request.Scheme}://{context.Request.Host}{issuerSubPath}", StringComparison.Ordinal))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                var requestUrl = $"{context.Request.Scheme}://{context.Request.Host}{issuerSubPath}";
                logger.OAuthDiscoveryRequestUriMismatch(
                    issuerUri.SanitizeLogParameter(),
                    requestUrl.SanitizeLogParameter());
            }
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        // generate response
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.CallingIntoOAuthDiscoveryResponseGenerator(ResponseGenerator.GetType().FullName);
        }

        return await GetDiscoveryDocument(context, baseUrl, issuerUri);
    }
}
