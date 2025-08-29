// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Endpoints;

internal abstract class BaseDiscoveryEndpoint(
    IdentityServerOptions options,
    IDiscoveryResponseGenerator responseGenerator)
{
    protected IdentityServerOptions Options = options;
    protected IDiscoveryResponseGenerator ResponseGenerator = responseGenerator;

    protected async Task<IEndpointResult> GetDiscoveryDocument(HttpContext context, string baseUrl, string issuerUri)
    {
        if (Options.Preview.EnableDiscoveryDocumentCache)
        {
            var distributedCache = context.RequestServices.GetRequiredService<IDistributedCache>();
            if (distributedCache is not null)
            {
                return await GetCachedDiscoveryDocument(distributedCache, baseUrl, issuerUri);
            }
            // fall through to default implementation if there is no cache provider registered
        }

        var response = await ResponseGenerator.CreateDiscoveryDocumentAsync(baseUrl, issuerUri);
        return new DiscoveryDocumentResult(response, Options.Discovery.ResponseCacheInterval);
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
                maxAge: Options.Discovery.ResponseCacheInterval
            );
        }

        var entries =
            await ResponseGenerator.CreateDiscoveryDocumentAsync(baseUrl, issuerUri);

        var expirationFromNow = Options.Preview.DiscoveryDocumentCacheDuration;

        var result =
            new DiscoveryDocumentResult(
                entries,
                isUsingPreviewFeature: true,
                maxAge: Options.Discovery.ResponseCacheInterval);

        await cache.SetStringAsync(key, result.Json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expirationFromNow,
        });

        return result;
    }
}
