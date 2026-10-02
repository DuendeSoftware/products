// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

internal class EndpointRouter(
    IEnumerable<Endpoint> endpoints,
    IdentityServerLicenseValidator licenseValidator,
    IdentityServerOptions options,
    ILogger<EndpointRouter> logger)
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
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.RequestPathPathMatchedToEndpointTypeEndpoint(
                        context.Request.Path.ToString().SanitizeLogParameter(),
                        endpointName);
                }

                licenseValidator.ValidateLicense();

                return GetEndpointHandler(endpoint, context);
            }
        }

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.NoEndpointEntryFoundForRequestPathPath(
                context.Request.Path.ToString().SanitizeLogParameter());
        }

        return null;
    }

    private IEndpointHandler GetEndpointHandler(Endpoint endpoint, HttpContext context)
    {
        if (options.Endpoints.IsEndpointEnabled(endpoint))
        {
            if (context.RequestServices.GetService(endpoint.Handler) is IEndpointHandler handler)
            {
                logger.EndpointEnabledEndpointSuccessfullyCreatedHandlerEndpointHandler(endpoint.Name, endpoint.Handler.FullName);
                return handler;
            }

            logger.EndpointEnabledEndpointFailedToCreateHandlerEndpointHandler(endpoint.Name, endpoint.Handler.FullName);
        }
        else
        {
            logger.EndpointDisabledEndpoint(endpoint.Name);
        }

        return null;
    }
}
