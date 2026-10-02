// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;


namespace Duende.IdentityServer.Endpoints;

internal class DiscoveryEndpoint : BaseDiscoveryEndpoint, IEndpointHandler
{
    private readonly ILogger _logger;

    private readonly IIssuerNameService _issuerNameService;
    private readonly IServerUrls _urls;

    public DiscoveryEndpoint(
        IdentityServerOptions options,
        IIssuerNameService issuerNameService,
        IDiscoveryResponseGenerator responseGenerator,
        IServerUrls urls,
        ILogger<DiscoveryEndpoint> logger) : base(options, responseGenerator)
    {
        _logger = logger;
        _issuerNameService = issuerNameService;
        _urls = urls;
    }

    public async Task<IEndpointResult> ProcessAsync(HttpContext context)
    {
        using var activity =
            Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.Discovery + "Endpoint");

        _logger.ProcessingDiscoveryRequest();

        // validate HTTP
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            _logger.DiscoveryEndpointOnlySupportsGETRequests();
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        _logger.StartDiscoveryRequest();

        if (!Options.Endpoints.EnableDiscoveryEndpoint)
        {
            _logger.DiscoveryEndpointDisabled404();
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        var baseUrl = _urls.BaseUrl;
        var issuerUri = await _issuerNameService.GetCurrentAsync(context.RequestAborted);

        // generate response
        _logger.CallingIntoDiscoveryResponseGenerator(ResponseGenerator.GetType().FullName);

        return await GetDiscoveryDocument(context, baseUrl, issuerUri);
    }
}
