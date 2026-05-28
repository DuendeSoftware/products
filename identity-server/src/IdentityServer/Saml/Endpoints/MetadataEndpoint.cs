// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Endpoints;

internal sealed class MetadataEndpoint(
    IServerUrls serverUrls,
    ISamlSigningService samlSigningService,
    ISaml2IssuerNameService saml2IssuerNameService,
    IOptions<IdentityServerOptions> identityServerOptions,
    ISaml2MetadataResponseGenerator metadataResponseGenerator)
    : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.SamlMetadata + "Endpoint");
        var options = identityServerOptions.Value;

        if (!options.Endpoints.EnableSamlMetadataEndpoint)
        {
            return new StatusCodeResult(StatusCodes.Status404NotFound);
        }

        // validate HTTP
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        var issuer = await saml2IssuerNameService.GetCurrentAsync(context.RequestAborted);
        var signingKeys = await samlSigningService.GetAllSigningCertificatesAsync(context.RequestAborted);
        var baseUrl = serverUrls.BaseUrl;

        return await metadataResponseGenerator.GenerateMetadataAsync(issuer, signingKeys, options.Saml, baseUrl, context.RequestAborted);
    }
}
