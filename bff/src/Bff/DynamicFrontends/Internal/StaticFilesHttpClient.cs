// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class StaticFilesHttpClient(
    IOptions<BffOptions> options,
    IHttpClientFactory clientFactory,
    CurrentFrontendAccessor currentFrontendAccessor,
    HybridCache cache,
    ILogger<StaticFilesHttpClient> logger,
    IIndexHtmlTransformer? transformer = null)
    : IStaticFilesClient, IAsyncDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    public async Task<string?> GetIndexHtmlAsync(CT ct = default)
    {
        var frontend = currentFrontendAccessor.Get();

        var cacheKey = BuildCacheKey(frontend);

        try
        {
            return await cache.GetOrCreateAsync(cacheKey, async (ct1) =>
                {
                    var client = clientFactory.CreateClient(options.Value.StaticAssetsClientName ??
                                                             Constants.HttpClientNames.StaticAssetsClientName);

                    var response = await client.GetAsync(frontend.CdnIndexHtmlUrl, ct1);
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        logger.IndexHtmlRetrievalFailed(LogLevel.Information, frontend.Name,
                            response.StatusCode);
                        throw new PreventCacheException();
                    }

                    var html = await response.Content.ReadAsStringAsync(ct1);

                    if (transformer == null)
                    {
                        return html;
                    }

                    logger.RetrievedIndexHTML(LogLevel.Information, frontend.Name, response.StatusCode);

                    var transformed = await transformer.Transform(html, ct1);
                    return transformed;
                },
                options: new HybridCacheEntryOptions()
                {
                    Expiration = options.Value.IndexHtmlDefaultCacheDuration
                },
                cancellationToken: ct);
        }
        catch (PreventCacheException)
        {
            return null;
        }
    }

    public async Task ProxyStaticAssetsAsync(HttpContext context, CT ct = default)
    {
        var frontend = currentFrontendAccessor.Get();

        var client = clientFactory.CreateClient(options.Value.StaticAssetsClientName ??
                                                 Constants.HttpClientNames.StaticAssetsClientName);

        var path = context.Request.Path.ToString() + context.Request.QueryString;

        var frontendStaticAssetsUrl = frontend.StaticAssetsUrl ??
                                      throw new InvalidOperationException("Static assets can't be proxied without the static assets url.");

        if (!frontendStaticAssetsUrl.AbsolutePath.EndsWith('/'))
        {
            frontendStaticAssetsUrl = new UriBuilder(frontendStaticAssetsUrl.Scheme, frontendStaticAssetsUrl.Host,
                    frontendStaticAssetsUrl.Port, frontendStaticAssetsUrl.AbsolutePath + "/",
                    frontendStaticAssetsUrl.Query)
                .Uri;
        }

        var requestUri = new Uri(frontendStaticAssetsUrl, path.TrimStart('/'));
        var response = await client.GetAsync(requestUri, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response = await client.GetAsync(frontendStaticAssetsUrl, ct);
        }

        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        var responseStream = await response.Content.ReadAsStreamAsync(ct);
        await responseStream.CopyToAsync(context.Response.Body, ct);

    }

    internal static string BuildCacheKey(BffFrontend frontend) => "Duende.Bff.IndexHtml:" + frontend.Name;

#pragma warning disable CA1032 // Do not use a custom message for this exception, as it is used to prevent caching
#pragma warning disable CA1064 // do not make this exception public as it's purely internal
    private class PreventCacheException : Exception
#pragma warning restore CA1064
#pragma warning restore CA1032
    {
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _stopping.Dispose();
    }
}
