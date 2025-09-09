// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class ProxyIndexMiddleware(RequestDelegate next,
    IStaticFilesClient staticFilesClient,
    CurrentFrontendAccessor currentFrontendAccessor)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var ct = context.RequestAborted;

        if (ShouldProxyIndexRoutes())
        {
            var indexHtml = await staticFilesClient.GetIndexHtmlAsync(ct);
            if (indexHtml != null)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(indexHtml, ct);
                return;
            }
        }

        if (ShouldProxyStaticContent())
        {
            await staticFilesClient.ProxyStaticAssetsAsync(context, ct);
            return;
        }
        await next(context);
    }

    private bool ShouldProxyIndexRoutes()
    {
        if (!currentFrontendAccessor.TryGet(out var frontend))
        {
            return false;
        }

        return (frontend.CdnIndexHtmlUrl != null);
    }

    private bool ShouldProxyStaticContent()
    {
        if (!currentFrontendAccessor.TryGet(out var frontend))
        {
            return false;
        }

        return (frontend.StaticAssetsUrl != null);
    }

}

