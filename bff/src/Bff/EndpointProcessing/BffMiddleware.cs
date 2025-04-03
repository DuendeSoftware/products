// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Duende.Bff.Endpoints;

/// <summary>
/// Middleware to provide anti-forgery protection via a static header and 302 to 401 conversion
/// Must run *before* the authorization middleware
/// </summary>
public class BffMiddleware(
    RequestDelegate next,
    IOptions<BffOptions> options,
    ILogger<BffMiddleware> logger)
{
    /// <summary>
    /// Request processing
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task Invoke(HttpContext context)
    {
        // add marker so we can determine if middleware has run later in the pipeline
        context.Items[Constants.BffMiddlewareMarker] = true;

        // inbound: add CSRF check for local APIs 

        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await next(context);
            return;
        }

        var isBffEndpoint = endpoint.Metadata.GetMetadata<IBffApiEndpoint>() != null;
        var requireAntiForgeryCheck = endpoint.Metadata.GetMetadata<IBffApiSkipAntiforgery>() == null;
        var hasAntiForgeryHeader = context.CheckAntiForgeryHeader(options.Value);
        if (isBffEndpoint && requireAntiForgeryCheck && !hasAntiForgeryHeader)
        {
            logger.AntiForgeryValidationFailed(context.Request.Path);

            context.Response.StatusCode = 401;
            return;
        }

        var isUiEndpoint = endpoint.Metadata.GetMetadata<IBffUIApiEndpoint>() != null;
        if (isUiEndpoint && context.IsAjaxRequest())
        {
            logger.LogDebug("BFF management endpoint {endpoint} is only intended for a browser window to request and load. It is not intended to be accessed with Ajax or fetch requests.", context.Request.Path);
        }

        await next(context);
    }
}
