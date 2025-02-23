// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading.Tasks;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;

namespace Duende.IdentityServer.Endpoints;

internal class CheckSessionEndpoint : IEndpointHandler
{
    private readonly ILogger _logger;

    public CheckSessionEndpoint(ILogger<CheckSessionEndpoint> logger)
    {
        _logger = logger;
    }

    public Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.CheckSession + "Endpoint");
        
        IEndpointResult result;

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            _logger.LogWarning("Invalid HTTP method for check session endpoint");
            result = new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }
        else
        {
            _logger.LogDebug("Rendering check session result");
            result = new CheckSessionResult();
        }

        return Task.FromResult(result);
    }
}