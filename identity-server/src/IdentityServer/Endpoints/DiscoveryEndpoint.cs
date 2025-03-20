// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Duende.IdentityServer.Endpoints;

internal class DiscoveryEndpoint : IEndpointHandler
{
    private readonly ILogger _logger;

    private readonly IdentityServerOptions _options;
    private readonly IIssuerNameService _issuerNameService;
    private readonly IServerUrls _urls;
    private readonly IDiscoveryResponseGenerator _responseGenerator;

    public DiscoveryEndpoint(
        IdentityServerOptions options,
        IIssuerNameService issuerNameService,
        IDiscoveryResponseGenerator responseGenerator,
        IServerUrls urls,
        ILogger<DiscoveryEndpoint> logger)
    {
        _logger = logger;
        _options = options;
        _issuerNameService = issuerNameService;
        _urls = urls;
        _responseGenerator = responseGenerator;
    }

    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity =
            Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.Discovery + "Endpoint");

        _logger.LogTrace("Processing discovery request.");

        // validate HTTP
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            _logger.LogWarning("Discovery endpoint only supports GET requests");
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        _logger.LogDebug("Start discovery request");

        if (!_options.Endpoints.EnableDiscoveryEndpoint)
        {
            _logger.LogInformation("Discovery endpoint disabled. 404.");
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        var baseUrl = _urls.BaseUrl;
        var issuerUri = await _issuerNameService.GetCurrentAsync();

        // generate response
        _logger.LogTrace("Calling into discovery response generator: {type}", _responseGenerator.GetType().FullName);

        if (_options.Preview.EnableDiscoveryDocumentCache)
        {
            var distributedCache = context.RequestServices.GetRequiredService<IDistributedCache>();
            if (distributedCache is not null)
            {
                return await GetCachedDiscoveryDocument(distributedCache, baseUrl, issuerUri);
            }
            // fall through to default implementation if there is no cache provider registered
        }

        var response = await _responseGenerator.CreateDiscoveryDocumentAsync(baseUrl, issuerUri);
        return new DiscoveryDocumentResult(response, _options.Discovery.ResponseCacheInterval);
    }

    private async Task<IEndpointResult> GetCachedDiscoveryDocument(IDistributedCache cache, string baseUrl,
        string issuerUri)
    {
        var key = $"discoveryDocument/{baseUrl}/{issuerUri}";
        var json = await cache.GetStringAsync(key);

        if (json is not null)
        {
            return new DiscoveryDocumentResult(
                json: json,
                maxAge: _options.Discovery.ResponseCacheInterval
            );
        }

        var entries =
            await _responseGenerator.CreateDiscoveryDocumentAsync(baseUrl, issuerUri);

        var expirationFromNow = _options.Preview.DiscoveryDocumentCacheDuration;

        var result =
            new DiscoveryDocumentResult(
                entries,
                isUsingPreviewFeature: true,
                maxAge: _options.Discovery.ResponseCacheInterval);

        await cache.SetStringAsync(key, result.Json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expirationFromNow,
        });

        return result;
    }
}
