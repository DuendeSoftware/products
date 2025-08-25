// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Concurrent;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;

namespace Duende.IdentityServer.Hosting;

internal class EndpointRouter(
    IEnumerable<Endpoint> endpoints,
    ProtocolRequestCounter requestCounter,
    LicenseExpirationChecker licenseExpirationChecker,
    IdentityServerOptions options,
    SanitizedLogger<EndpointRouter> sanitizedLogger)
    : IEndpointRouter
{
    private readonly ConcurrentDictionary<string, TemplateMatcher> _routeCache = new();

    public IEndpointHandler Find(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var endpoint in endpoints)
        {
            var path = endpoint.Path;
            if (context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                var endpointName = endpoint.Name;
                sanitizedLogger.LogDebug("Request path {path} matched to endpoint type {endpoint}", context.Request.Path, endpointName);

                requestCounter.Increment();
                licenseExpirationChecker.CheckExpiration();

                return GetEndpointHandler(endpoint, context);
            }

            if (endpoint.UsesRouteTemplate && endpoint.Path.HasValue)
            {
                if (!_routeCache.TryGetValue(endpoint.Path.Value, out var matcher))
                {
                    var routePattern = RoutePatternFactory.Parse(endpoint.Path.Value.RemoveLeadingSlash()!);
                    var template = TemplateParser.Parse(routePattern.RawText!);
                    matcher = new TemplateMatcher(template, new RouteValueDictionary());
                    _routeCache.TryAdd(endpoint.Path.Value, matcher);
                }

                var matchedValues = new RouteValueDictionary();
                if (matcher.TryMatch(context.Request.Path, matchedValues) && (endpoint.OnRouteMatched == null || endpoint.OnRouteMatched(context, matchedValues, sanitizedLogger.ToILogger())))
                {
                    var endpointName = endpoint.Name;
                    sanitizedLogger.LogDebug("Request path {path} matched to endpoint type {endpoint}", context.Request.Path, endpointName);

                    requestCounter.Increment();
                    licenseExpirationChecker.CheckExpiration();

                    return GetEndpointHandler(endpoint, context);
                }
            }
        }

        sanitizedLogger.LogTrace("No endpoint entry found for request path: {path}", context.Request.Path);

        return null;
    }

    private IEndpointHandler GetEndpointHandler(Endpoint endpoint, HttpContext context)
    {
        if (options.Endpoints.IsEndpointEnabled(endpoint))
        {
            if (context.RequestServices.GetService(endpoint.Handler) is IEndpointHandler handler)
            {
                sanitizedLogger.LogDebug("Endpoint enabled: {endpoint}, successfully created handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
                return handler;
            }

            sanitizedLogger.LogDebug("Endpoint enabled: {endpoint}, failed to create handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
        }
        else
        {
            sanitizedLogger.LogWarning("Endpoint disabled: {endpoint}", endpoint.Name);
        }

        return null;
    }
}
