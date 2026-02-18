// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml.Linq;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.Metadata.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.Metadata;

internal class SamlMetaDataEndpoint(
    TimeProvider timeProvider,
    IOptions<SamlOptions> samlOptions,
    IIssuerNameService issuerNameService,
    IServerUrls urls,
    ISamlSigningService samlSigningService) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("SamlMetaDataEndpoint");

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return new StatusCodeResult(System.Net.HttpStatusCode.MethodNotAllowed);
        }

        var options = samlOptions.Value;
        var issuerUri = await issuerNameService.GetCurrentAsync();
        var baseUrl = urls.BaseUrl;

        var certificateBase64 = await samlSigningService.GetSigningCertificateBase64Async();

        var singleSignOnService = BuildServiceUrl(baseUrl, options.UserInteraction.Route, options.UserInteraction.SignInPath);
        var singleLogoutService = BuildServiceUrl(baseUrl, options.UserInteraction.Route, options.UserInteraction.SingleLogoutPath);

        var descriptor = new EntityDescriptor
        {
            EntityId = issuerUri,
            ValidUntil = options.MetadataValidityDuration != null
                ? timeProvider.GetUtcNow().Add(options.MetadataValidityDuration.Value).UtcDateTime
                : null,
            IdpSsoDescriptor = new IdpSsoDescriptor
            {
                ProtocolSupportEnumeration = SamlConstants.Namespaces.Protocol,
                WantAuthnRequestsSigned = options.WantAuthnRequestsSigned,
                KeyDescriptors =
                [
                    new KeyDescriptor
                    {
                        Use = KeyUse.Signing,
                        X509Certificate = certificateBase64
                    }
                ],
                NameIdFormats = options.SupportedNameIdFormats,
                SingleSignOnServices =
                [
                    new SingleSignOnService
                    {
                        Binding = SamlBinding.HttpPost,
                        Location = singleSignOnService
                    },
                    new SingleSignOnService
                    {
                        Binding = SamlBinding.HttpRedirect,
                        Location = singleSignOnService
                    }
                ],
                SingleLogoutServices =
                [
                    new SingleLogoutService
                    {
                        Binding = SamlBinding.HttpPost,
                        Location = singleLogoutService
                    },
                    new SingleLogoutService
                    {
                        Binding = SamlBinding.HttpRedirect,
                        Location = singleLogoutService
                    }
                ]
            }
        };

        return new SamlMetadataResult(descriptor);
    }

    private static Uri BuildServiceUrl(string baseUrl, string route, string path)
    {
        var builder = new UriBuilder(baseUrl);

        // Preserve existing base path and append new segments
        var segments = new[] { builder.Path, route, path }
            .Select(s => s.Trim('/'))
            .Where(s => !string.IsNullOrWhiteSpace(s));

        var combinedPath = string.Join('/', segments);

        // UriBuilder.Path automatically adds leading slash
        builder.Path = string.IsNullOrEmpty(combinedPath) ? "/" : combinedPath;

        return builder.Uri;
    }
}

/// <summary>
/// Endpoint result that writes SAML metadata XML to the response.
/// </summary>
internal class SamlMetadataResult(EntityDescriptor descriptor) : IEndpointResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = SamlConstants.ContentTypes.Metadata;
        var descriptorXml = EntityDescriptorSerializer.SerializeToXml(descriptor);
        await descriptorXml.SaveAsync(context.Response.Body, SaveOptions.DisableFormatting, context.RequestAborted);
    }
}
