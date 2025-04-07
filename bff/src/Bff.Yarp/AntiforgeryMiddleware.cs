// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Yarp;

/// <summary>
/// Middleware for YARP to check the antiforgery header
/// </summary>
internal class AntiforgeryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly BffOptions _options;
    private readonly ILogger<AntiforgeryMiddleware> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="next"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public AntiforgeryMiddleware(RequestDelegate next, IOptions<BffOptions> options, ILogger<AntiforgeryMiddleware> logger)
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


        if (route.Config.Metadata != null)
        {
            if (route.Config.Metadata.TryGetValue(Constants.Yarp.AntiforgeryCheckMetadata, out var value))
            {
                if (string.Equals(value, true.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (!context.CheckAntiForgeryHeader(_options))
                    {
                        context.Response.StatusCode = 401;
                        _logger.AntiForgeryValidationFailed(route.Config.RouteId);

                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
