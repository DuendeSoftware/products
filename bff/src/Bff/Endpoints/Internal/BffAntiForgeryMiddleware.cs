// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Middleware to provide anti-forgery protection via a static header and 302 to 401 conversion
/// Must run *before* the authorization middleware
/// </summary>
internal class BffAntiForgeryMiddleware(
    RequestDelegate next,
    IOptions<BffOptions> options,
    ILogger<BffAntiForgeryMiddleware> logger)
{
    /// <summary>
    /// Request processing
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // add marker so we can determine if middleware has run later in the pipeline
        context.Items[Constants.Middleware.AntiForgeryMarker] = true;

        if (options.Value.DisableAntiForgeryCheck(context))
        {
            await next(context);
            return;
        }

        // inbound: add CSRF check for local APIs 

        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await next(context);
            return;
        }

        var isBffEndpoint = endpoint.Metadata.GetMetadata<IBffApiMetadata>() != null;
        var requireAntiForgeryCheck = endpoint.Metadata.GetMetadata<IBffApiSkipAntiForgery>() == null;
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
            logger.ManagementEndpointAccessedViaAjax(LogLevel.Debug, context.Request.Path.Sanitize());
        }

        await next(context);
    }
}
