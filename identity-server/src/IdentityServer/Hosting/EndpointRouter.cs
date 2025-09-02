// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Logging;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Hosting;

internal class EndpointRouter(
    IEnumerable<Endpoint> endpoints,
    ProtocolRequestCounter requestCounter,
    LicenseExpirationChecker licenseExpirationChecker,
    IdentityServerOptions options,
    SanitizedLogger<EndpointRouter> sanitizedLogger)
    : IEndpointRouter
{
    public IEndpointHandler Find(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var endpoint in endpoints)
        {
            if (endpoint.IsMatch(context))
            {
                var endpointName = endpoint.Name;
                sanitizedLogger.LogDebug("Request path {path} matched to endpoint type {endpoint}", context.Request.Path, endpointName);

                requestCounter.Increment();
                licenseExpirationChecker.CheckExpiration();

                return GetEndpointHandler(endpoint, context);
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
