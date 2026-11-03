// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Yarp.Internal;

/// <summary>
/// Middleware for YARP to check the antiforgery header
/// </summary>
internal class AntiForgeryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly BffOptions _options;
    private readonly ILogger<AntiForgeryMiddleware> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="next"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public AntiForgeryMiddleware(RequestDelegate next, IOptions<BffOptions> options, ILogger<AntiForgeryMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Get invoked for YARP requests
    /// </summary>
    /// <param name="context"></param>
    public async Task Invoke(HttpContext context)
    {
        var route = context.GetRouteModel();

        // Check if the request is a WebSocket request
        if (_options.DisableAntiForgeryCheck(context))
        {
            await _next(context);
            return;
        }

        if (route.Config.HasAntiforgeryCheck())
        {
            if (!context.CheckAntiForgeryHeader(_options))
            {
                context.Response.StatusCode = 401;
                _logger.AntiForgeryValidationFailed(LogLevel.Warning, route.Config.RouteId);

                return;
            }

            // Record that the header was checked for this endpoint, so the YARP access token
            // transform can tell that proxyApp.UseAntiforgeryCheck() is in the pipeline.
            context.Items[Constants.Middleware.YarpAntiForgeryCheckedEndpoint] = context.GetEndpoint();
        }

        await _next(context);
    }
}
