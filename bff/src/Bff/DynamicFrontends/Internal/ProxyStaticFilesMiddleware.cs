// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class ProxyStaticFilesMiddleware(RequestDelegate next,
    IStaticFilesClient staticFilesClient,
    CurrentFrontendAccessor currentFrontendAccessor)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var ct = context.RequestAborted;

        // What about 'head' requests?
        if (context.Request.Method != HttpMethods.Get)
        {
            await next(context);
            return;
        }

        // At this point in the HTTP Pipeline, all matched routes have already been processed
        // So, it's effectively an unmatched route. 
        if (ShouldProxyIndexHtml())
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
        else if (ShouldProxyStaticContent())
        {
            await staticFilesClient.ProxyStaticAssetsAsync(context, ct);
            return;
        }
        await next(context);
    }

    private bool ShouldProxyIndexHtml()
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

