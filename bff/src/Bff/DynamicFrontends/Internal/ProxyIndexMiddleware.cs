// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class ProxyIndexMiddleware(RequestDelegate next,
    IIndexHtmlClient indexHtmlClient,
    SelectedFrontend selectedFrontend)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldProxyIndexRoutes())
        {
            var ct = context.RequestAborted;
            var indexHtml = await indexHtmlClient.GetIndexHtmlAsync(ct);
            if (indexHtml != null)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(indexHtml, ct);
            }
        }

        await next(context);
    }

    private bool ShouldProxyIndexRoutes()
    {
        if (!selectedFrontend.TryGet(out var frontend))
        {
            return false;
        }

        return (frontend.IndexHtmlUrl != null);
    }
}
