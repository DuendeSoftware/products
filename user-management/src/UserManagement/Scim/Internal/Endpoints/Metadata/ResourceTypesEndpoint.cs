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
/// Handles GET /scim/ResourceTypes and GET /scim/ResourceTypes/{id} (RFC 7644 §4).
/// Returns the set of SCIM resource types supported by this server.
/// </summary>
internal sealed class ResourceTypesEndpoint
{
    private readonly ScimCapabilityResolver _capabilities;
    private readonly ScimEndpointOptions _options;
    private readonly IServerUrls _serverUrls;
    private readonly ILogger<ResourceTypesEndpoint> _logger;

    public ResourceTypesEndpoint(
        ScimCapabilityResolver capabilities,
        IServerUrls serverUrls,
        IOptions<ScimEndpointOptions> options,
        ILogger<ResourceTypesEndpoint> logger)
    {
        _capabilities = capabilities;
        _serverUrls = serverUrls;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>GET /scim/ResourceTypes — list all resource types.</summary>
    public IResult HandleList(HttpContext ctx)
    {
        _logger.ScimResourceTypesAccessed(LogLevel.Debug);
        var resourceTypes = BuildResourceTypes(ctx);
        var response = new ScimMetadataListResponse<ScimResourceType>
        {
            TotalResults = resourceTypes.Count,
            Resources = resourceTypes
        };
        return ScimResults.Ok(response);
    }

    /// <summary>GET /scim/ResourceTypes/{id} — get a single resource type by name.</summary>
    public IResult HandleGet(string id, HttpContext ctx)
    {
        _logger.ScimResourceTypesAccessed(LogLevel.Debug);
        var resourceTypes = BuildResourceTypes(ctx);
        var match = resourceTypes.Find(rt =>
            string.Equals(rt.Id, id, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return ScimResults.Error(404, "Resource type not found.");
        }

        return ScimResults.Ok(match);
    }

    private List<ScimResourceType> BuildResourceTypes(HttpContext ctx)
    {
        var baseUrl = _serverUrls.Origin + (ctx.Request.PathBase.Value ?? string.Empty);
        var metadataRoute = _options.MetadataRoute.TrimStart('/');
        var usersEndpoint = _options.Route;
        var result = new List<ScimResourceType>();

        if (_capabilities.UsersEnabled)
        {
            result.Add(new ScimResourceType
            {
                Id = ScimConstants.ResourceTypes.User,
                Name = ScimConstants.ResourceTypes.User,
                Description = "User account.",
                Endpoint = usersEndpoint,
                Schema = ScimConstants.UserSchemaUrn,
                Meta = new ScimMeta
                {
                    ResourceType = ScimConstants.ResourceTypes.ResourceType,
                    Location = $"{baseUrl}/{metadataRoute}/ResourceTypes/{ScimConstants.ResourceTypes.User}"
                }
            });
        }

        if (_capabilities.GroupsEnabled)
        {
            result.Add(new ScimResourceType
            {
                Id = ScimConstants.ResourceTypes.Group,
                Name = ScimConstants.ResourceTypes.Group,
                Description = "Group of users.",
                Endpoint = "/Groups",
                Schema = ScimConstants.GroupSchemaUrn,
                Meta = new ScimMeta
                {
                    ResourceType = ScimConstants.ResourceTypes.ResourceType,
                    Location = $"{baseUrl}/{metadataRoute}/ResourceTypes/{ScimConstants.ResourceTypes.Group}"
                }
            });
        }

        return result;
    }
}
