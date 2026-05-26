// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Metadata;

/// <summary>
/// Handles GET /scim/ServiceProviderConfig (RFC 7644 §4).
/// Returns a single resource describing the SCIM service provider capabilities.
/// </summary>
internal sealed class ServiceProviderConfigEndpoint
{
    private readonly ScimCapabilityResolver _capabilities;
    private readonly ScimEndpointOptions _options;
    private readonly IServerUrls _serverUrls;
    private readonly ILogger<ServiceProviderConfigEndpoint> _logger;

    public ServiceProviderConfigEndpoint(
        ScimCapabilityResolver capabilities,
        IServerUrls serverUrls,
        IOptions<ScimEndpointOptions> options,
        ILogger<ServiceProviderConfigEndpoint> logger)
    {
        _capabilities = capabilities;
        _serverUrls = serverUrls;
        _options = options.Value;
        _logger = logger;
    }

    public IResult Handle(HttpContext ctx)
    {
        _logger.ScimServiceProviderConfigAccessed(LogLevel.Debug);

        var baseUrl = _serverUrls.Origin + (ctx.Request.PathBase.Value ?? string.Empty);
        var metadataRoute = _options.MetadataRoute.TrimStart('/');

        var config = new ScimServiceProviderConfig
        {
            DocumentationUri = "https://docs.duendesoftware.com/scim",
            Patch = new ScimSupported { Supported = true },
            Bulk = new ScimBulkSupported
            {
                Supported = true,
                MaxOperations = _capabilities.MaxBulkOperations,
                MaxPayloadSize = _capabilities.MaxBulkPayloadSize
            },
            Filter = new ScimFilterSupported { Supported = true, MaxResults = _capabilities.MaxResults },
            ChangePassword = new ScimSupported { Supported = _capabilities.ChangePasswordSupported },
            Sort = new ScimSupported { Supported = true },
            ETag = new ScimSupported { Supported = true },
            AuthenticationSchemes =
            [
                new ScimAuthenticationScheme
                {
                    Type = "oauthbearertoken",
                    Name = "OAuth Bearer Token",
                    Description = "Authentication scheme using the OAuth Bearer Token standard.",
                    SpecUri = "https://www.rfc-editor.org/info/rfc6750"
                }
            ],
            Meta = new ScimMeta
            {
                ResourceType = ScimConstants.ResourceTypes.ServiceProviderConfig,
                Location = $"{baseUrl}/{metadataRoute}/{ScimConstants.ResourceTypes.ServiceProviderConfig}"
            }
        };

        return ScimResults.Ok(config);
    }
}
