// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.v2;

namespace Duende.IdentityServer.Hosting;

internal class EndpointRouter : IEndpointRouter
{
    private readonly IEnumerable<Endpoint> _endpoints;
    private readonly IProtocolRequestCounter _requestCounter;
    private readonly IdentityServerOptions _options;
    private readonly ILogger _logger;

    public EndpointRouter(
        IEnumerable<Endpoint> endpoints,
        IProtocolRequestCounter requestCounter,
        IdentityServerOptions options, 
        ILogger<EndpointRouter> logger)
    {
        _endpoints = endpoints;
        _requestCounter = requestCounter;
        _options = options;
        _logger = logger;
    }

    public IEndpointHandler Find(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach(var endpoint in _endpoints)
        {
            var path = endpoint.Path;
            if (context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                var endpointName = endpoint.Name;
                _logger.LogDebug("Request path {path} matched to endpoint type {endpoint}", context.Request.Path, endpointName);

                _requestCounter.Increment();
                return GetEndpointHandler(endpoint, context);
            }
        }

        _logger.LogTrace("No endpoint entry found for request path: {path}", context.Request.Path);

        return null;
    }

    private IEndpointHandler GetEndpointHandler(Endpoint endpoint, HttpContext context)
    {
        if (_options.Endpoints.IsEndpointEnabled(endpoint))
        {
            if (context.RequestServices.GetService(endpoint.Handler) is IEndpointHandler handler)
            {
                _logger.LogDebug("Endpoint enabled: {endpoint}, successfully created handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
                return handler;
            }

            _logger.LogDebug("Endpoint enabled: {endpoint}, failed to create handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
        }
        else
        {
            _logger.LogWarning("Endpoint disabled: {endpoint}", endpoint.Name);
        }

        return null;
    }
}