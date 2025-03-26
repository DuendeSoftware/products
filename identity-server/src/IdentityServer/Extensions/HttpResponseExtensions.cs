// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;

#pragma warning disable 1591

namespace Duende.IdentityServer.Extensions;

public static class HttpResponseExtensions
{
    public static async Task WriteJsonAsync(this HttpResponse response, object o, string contentType = null)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("WriteJson");

        var json = ObjectSerializer.ToString(o);
        await response.WriteJsonAsync(json, contentType);
    }

    public static async Task WriteJsonAsync(this HttpResponse response, string json, string contentType = null)
    {
        response.ContentType = contentType ?? "application/json; charset=UTF-8";
        await response.WriteAsync(json);
        await response.Body.FlushAsync();
    }

    public static void SetCache(this HttpResponse response, int maxAge, params string[] varyBy)
    {
        if (maxAge == 0)
        {
            SetNoCache(response);
        }
        else if (maxAge > 0)
        {
            if (!response.Headers.ContainsKey("Cache-Control"))
            {
                response.Headers.Append("Cache-Control", $"max-age={maxAge}");
            }

            if (varyBy?.Any() == true)
            {
                var vary = varyBy.Aggregate((x, y) => x + ',' + y);
                if (response.Headers.ContainsKey("Vary"))
                {
                    vary = response.Headers.Vary.ToString() + ',' + vary;
                }
                response.Headers.Vary = vary;
            }
        }
    }

    public static void SetNoCache(this HttpResponse response)
    {
        if (!response.Headers.ContainsKey("Cache-Control"))
        {
            response.Headers.Append("Cache-Control", "no-store, no-cache, max-age=0");
        }
        else
        {
            response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        }

        if (!response.Headers.ContainsKey("Pragma"))
        {
            response.Headers.Append("Pragma", "no-cache");
        }
    }

    public static async Task WriteHtmlAsync(this HttpResponse response, string html)
    {
        response.ContentType = "text/html; charset=UTF-8";
        await response.WriteAsync(html, Encoding.UTF8);
        await response.Body.FlushAsync();
    }

    public static void AddScriptCspHeaders(this HttpResponse response, CspOptions options, string hash)
    {
        var csp1part = options.Level == CspLevel.One ? "'unsafe-inline' " : string.Empty;
        var cspHeader = $"default-src 'none'; script-src {csp1part}'{hash}'";

        AddCspHeaders(response.Headers, options, cspHeader);
    }

    public static void AddStyleCspHeaders(this HttpResponse response, CspOptions options, string hash, string frameSources)
    {
        var csp1part = options.Level == CspLevel.One ? "'unsafe-inline' " : string.Empty;
        var cspHeader = $"default-src 'none'; style-src {csp1part}'{hash}'";

        if (!string.IsNullOrEmpty(frameSources))
        {
            cspHeader += $"; frame-src {frameSources}";
        }

        AddCspHeaders(response.Headers, options, cspHeader);
    }

    public static void AddCspHeaders(IHeaderDictionary headers, CspOptions options, string cspHeader)
    {
        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            headers.Append("Content-Security-Policy", cspHeader);
        }
        if (options.AddDeprecatedHeader && !headers.ContainsKey("X-Content-Security-Policy"))
        {
            headers.Append("X-Content-Security-Policy", cspHeader);
        }
    }
}
