// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Mime;
using System.Text;
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
    private int _clientNameCollisionWarningLogged;

    public async Task<string?> GetIndexHtmlAsync(CT ct = default)
    {
        var frontend = currentFrontendAccessor.Get();

        var cacheKey = BuildCacheKey(frontend);

        try
        {
            return await cache.GetOrCreateAsync(cacheKey, async (ctInner) =>
                {
                    var client = clientFactory.CreateClient(options.Value.StaticAssetsClientName);

                    var response = await client.GetAsync(frontend.CdnIndexHtmlUrl, ctInner);
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        logger.IndexHtmlRetrievalFailed(LogLevel.Warning, frontend.Name,
                            response.StatusCode);
                        throw new PreventCacheException();
                    }

                    var html = await response.Content.ReadAsStringAsync(ctInner);

                    if (transformer == null)
                    {
                        return html;
                    }

                    logger.RetrievedIndexHTML(LogLevel.Debug, frontend.Name, response.StatusCode);

                    var transformed = await transformer.Transform(html, frontend, ctInner);
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
        var frontendStaticAssetsUrl = frontend.StaticAssetsUrl ??
                                      throw new InvalidOperationException("Static assets can't be proxied without the static assets url.");

        if (!TryBuildRequestUri(frontendStaticAssetsUrl, context.Request.Path, context.Request.QueryString, out var requestUri))
        {
            logger.StaticAssetProxyPathRejected(LogLevel.Warning, frontend.Name);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var clientName = Constants.HttpClientNames.StaticAssetsProxyClientName;
        if (string.Equals(options.Value.StaticAssetsClientName, clientName, StringComparison.Ordinal))
        {
            clientName = Constants.HttpClientNames.StaticAssetsProxyFallbackClientName;
            if (Interlocked.Exchange(ref _clientNameCollisionWarningLogged, 1) == 0)
            {
                logger.StaticAssetProxyClientNameCollision(LogLevel.Warning,
                    Constants.HttpClientNames.StaticAssetsProxyClientName);
            }
        }
        using var client = clientFactory.CreateClient(clientName);

        var responseUri = requestUri;
        var response = await client.GetAsync(requestUri, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // If we can't find the file, try to request the root (this allows for SPAs with client side routing)
            response.Dispose();
            responseUri = frontendStaticAssetsUrl;
            response = await client.GetAsync(frontendStaticAssetsUrl, ct);
        }

        using (response)
        {
            if (IsRedirect(response.StatusCode))
            {
                logger.StaticAssetProxyRedirectRejected(LogLevel.Warning, frontend.Name, response.StatusCode);
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers)
            {
                if (IsFilteredUpstreamHeader(header.Key))
                {
                    continue;
                }

                var values = header.Value.ToArray();
                if (header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase)
                    && (values.Length != 1 || !IsAllowedLocation(frontendStaticAssetsUrl, responseUri, response.Headers.Location)))
                {
                    continue;
                }

                CopyUpstreamHeader(context.Response.Headers, header.Key, values);
            }
            foreach (var header in response.Content.Headers)
            {
                if (!IsFilteredUpstreamHeader(header.Key))
                {
                    CopyUpstreamHeader(context.Response.Headers, header.Key, header.Value.ToArray());
                }
            }

            if (response.RequestMessage?.RequestUri == frontendStaticAssetsUrl
                && response.StatusCode == HttpStatusCode.OK
                && response.Content.Headers.ContentType?.MediaType == MediaTypeNames.Text.Html
                && transformer != null)
            {
                var html = await response.Content.ReadAsStringAsync(ct);

                html = await transformer.Transform(html, frontend, ct) ?? string.Empty;
                context.Response.ContentLength = Encoding.UTF8.GetByteCount(html);
                await context.Response.WriteAsync(html, ct);
                return;
            }

            var responseStream = await response.Content.ReadAsStreamAsync(ct);
            await responseStream.CopyToAsync(context.Response.Body, ct);
        }
    }

    private static bool TryBuildRequestUri(Uri baseUri, PathString requestPath, QueryString queryString, out Uri requestUri)
    {
        requestUri = baseUri;
        if (!IsSafeRequestPath(requestPath.Value ?? "/"))
        {
            return false;
        }

        var relativePath = requestPath.ToUriComponent().TrimStart('/');
        if (relativePath.Length == 0 && !queryString.HasValue)
        {
            return true;
        }

        var pathAndQuery = baseUri.GetLeftPart(UriPartial.Path) + relativePath + queryString.ToUriComponent();
        if (!Uri.TryCreate(pathAndQuery, UriKind.Absolute, out var target)
            || !IsSameOrigin(baseUri, target)
            || !target.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.Ordinal))
        {
            return false;
        }

        requestUri = target;
        return true;
    }

    private static bool IsSafeRequestPath(string path)
    {
        foreach (var segment in path.Split('/'))
        {
            var decoded = segment;
            for (var decodeCount = 0; decodeCount < 16; decodeCount++)
            {
                if (decoded is "." or ".."
                    || decoded.Contains('/', StringComparison.Ordinal)
                    || decoded.Contains('\\', StringComparison.Ordinal))
                {
                    return false;
                }

                var next = Uri.UnescapeDataString(decoded);
                if (next == decoded)
                {
                    break;
                }

                decoded = next;
                if (decodeCount == 15)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsSameOrigin(Uri baseUri, Uri target) =>
        target.IsAbsoluteUri
        && string.Equals(baseUri.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(baseUri.Host, target.Host, StringComparison.OrdinalIgnoreCase)
        && baseUri.Port == target.Port;

    private static bool IsAllowedLocation(Uri baseUri, Uri requestUri, Uri? location) =>
        location is not null
        && !location.OriginalString.Contains('\\', StringComparison.Ordinal)
        && !location.OriginalString.Any(char.IsControl)
        && Uri.TryCreate(requestUri, location, out var target)
        && string.IsNullOrEmpty(target.UserInfo)
        && IsSameOrigin(baseUri, target);

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MultipleChoices or HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.UseProxy
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private bool IsFilteredUpstreamHeader(string headerName) =>
        headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
        || (options.Value.RemoveCookieHeadersFromProxiedResponses && IsSiteControlHeader(headerName));

    private static bool IsSiteControlHeader(string headerName) =>
        headerName.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Clear-Site-Data", StringComparison.OrdinalIgnoreCase);

    private static void CopyUpstreamHeader(IHeaderDictionary responseHeaders, string headerName, string[] values)
    {
        if (IsSiteControlHeader(headerName))
        {
            responseHeaders.Append(headerName, values);
        }
        else
        {
            responseHeaders[headerName] = values;
        }
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
