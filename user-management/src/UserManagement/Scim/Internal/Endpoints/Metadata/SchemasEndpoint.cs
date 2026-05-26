// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Metadata;

/// <summary>
/// Handles GET /scim/Schemas and GET /scim/Schemas/{id} (RFC 7644 §4).
/// Returns SCIM schema definitions including dynamic user profile attributes.
/// </summary>
internal sealed class SchemasEndpoint
{
    private readonly ScimCapabilityResolver _capabilities;
    private readonly ScimEndpointOptions _options;
    private readonly IUserProfileAdmin? _profileAdmin;
    private readonly IScimSchemaMapper _schemaMapper;
    private readonly IServerUrls _serverUrls;
    private readonly ILogger<SchemasEndpoint> _logger;

    public SchemasEndpoint(
        ScimCapabilityResolver capabilities,
        IServerUrls serverUrls,
        IScimSchemaMapper schemaMapper,
        IOptions<ScimEndpointOptions> options,
        ILogger<SchemasEndpoint> logger)
        : this(capabilities, serverUrls, schemaMapper, options, logger, profileAdmin: null)
    {
    }

    public SchemasEndpoint(
        ScimCapabilityResolver capabilities,
        IServerUrls serverUrls,
        IScimSchemaMapper schemaMapper,
        IOptions<ScimEndpointOptions> options,
        ILogger<SchemasEndpoint> logger,
        IUserProfileAdmin? profileAdmin)
    {
        _capabilities = capabilities;
        _serverUrls = serverUrls;
        _schemaMapper = schemaMapper;
        _options = options.Value;
        _logger = logger;
        _profileAdmin = profileAdmin;
    }

    /// <summary>GET /scim/Schemas — list all schemas.</summary>
    public async Task<IResult> HandleListAsync(HttpContext ctx, Ct ct)
    {
        _logger.ScimSchemasAccessed(LogLevel.Debug);

        var schemas = await BuildSchemasAsync(ctx, ct);
        var response = new ScimMetadataListResponse<ScimSchemaDefinition>
        {
            TotalResults = schemas.Count,
            Resources = schemas
        };
        return ScimResults.Ok(response);
    }

    /// <summary>GET /scim/Schemas/{id} — get a single schema by URN.</summary>
    public async Task<IResult> HandleGetAsync(string id, HttpContext ctx, Ct ct)
    {
        _logger.ScimSchemasAccessed(LogLevel.Debug);

        var schemas = await BuildSchemasAsync(ctx, ct);
        var match = schemas.Find(s =>
            string.Equals(s.Id, id, StringComparison.Ordinal));

        if (match is null)
        {
            return ScimResults.Error(404, "Schema not found.");
        }

        return ScimResults.Ok(match);
    }

    private async Task<List<ScimSchemaDefinition>> BuildSchemasAsync(HttpContext ctx, Ct ct)
    {
        var baseUrl = _serverUrls.Origin + (ctx.Request.PathBase.Value ?? string.Empty);
        var metadataRoute = _options.MetadataRoute.TrimStart('/');
        var result = new List<ScimSchemaDefinition>();

        if (_capabilities.UsersEnabled)
        {
            result.Add(await BuildUserSchemaAsync(baseUrl, metadataRoute, ct));
        }

        if (_capabilities.GroupsEnabled)
        {
            result.Add(BuildGroupSchema(baseUrl, metadataRoute));
        }

        return result;
    }

    private async Task<ScimSchemaDefinition> BuildUserSchemaAsync(string baseUrl, string metadataRoute, Ct ct)
    {
        // Fixed SCIM User attributes (RFC 7643 §4.1)
        var attributes = new List<ScimSchemaAttribute>
        {
            new()
            {
                Name = ScimConstants.Attributes.UserName,
                Type = ScimConstants.DataTypes.String,
                MultiValued = false,
                Description = "Unique identifier for the User, typically used by the user to directly authenticate.",
                Required = true,
                CaseExact = false,
                Mutability = ScimConstants.MutabilityValues.ReadWrite,
                Returned = ScimConstants.ReturnedValues.Default,
                Uniqueness = ScimConstants.UniquenessValues.Server
            }
        };

        // Only include the password attribute if changePassword is supported
        if (_capabilities.ChangePasswordSupported)
        {
            attributes.Add(new ScimSchemaAttribute
            {
                Name = ScimConstants.Attributes.Password,
                Type = ScimConstants.DataTypes.String,
                MultiValued = false,
                Description = "The User's cleartext password. Write-only.",
                Required = false,
                CaseExact = false,
                Mutability = ScimConstants.MutabilityValues.WriteOnly,
                Returned = ScimConstants.ReturnedValues.Never,
                Uniqueness = ScimConstants.UniquenessValues.None
            });
        }

        // Dynamic attributes from the user profile schema
        if (_profileAdmin is not null)
        {
            var schema = await _profileAdmin.GetSchemaAsync(ct);
            foreach (var definition in schema.AttributeDefinitions.Values)
            {
                var mapped = _schemaMapper.Map(definition);
                attributes.Add(new ScimSchemaAttribute
                {
                    Name = mapped.Name,
                    Type = mapped.Type,
                    MultiValued = mapped.MultiValued,
                    Description = mapped.Description,
                    Required = mapped.Required,
                    CaseExact = mapped.CaseExact,
                    Mutability = mapped.Mutability,
                    Returned = mapped.Returned,
                    Uniqueness = mapped.Uniqueness
                });
            }
        }

        return new ScimSchemaDefinition
        {
            Id = ScimConstants.UserSchemaUrn,
            Name = ScimConstants.ResourceTypes.User,
            Description = "User account.",
            Attributes = attributes,
            Meta = new ScimMeta
            {
                ResourceType = ScimConstants.ResourceTypes.Schema,
                Location = $"{baseUrl}/{metadataRoute}/Schemas/{ScimConstants.UserSchemaUrn}"
            }
        };
    }

    private static ScimSchemaDefinition BuildGroupSchema(string baseUrl, string metadataRoute) =>
        new()
        {
            Id = ScimConstants.GroupSchemaUrn,
            Name = ScimConstants.ResourceTypes.Group,
            Description = "Group of users.",
            Attributes =
            [
                new ScimSchemaAttribute
                {
                    Name = "displayName",
                    Type = ScimConstants.DataTypes.String,
                    MultiValued = false,
                    Description = "A human-readable name for the Group.",
                    Required = true,
                    CaseExact = false,
                    Mutability = ScimConstants.MutabilityValues.ReadWrite,
                    Returned = ScimConstants.ReturnedValues.Default,
                    Uniqueness = ScimConstants.UniquenessValues.None
                },
                new ScimSchemaAttribute
                {
                    Name = "members",
                    Type = ScimConstants.DataTypes.Complex,
                    MultiValued = true,
                    Description = "A list of members of the Group.",
                    Required = false,
                    Mutability = ScimConstants.MutabilityValues.ReadWrite,
                    Returned = ScimConstants.ReturnedValues.Default,
                    Uniqueness = ScimConstants.UniquenessValues.None,
                    SubAttributes =
                    [
                        new ScimSchemaAttribute
                        {
                            Name = ScimConstants.Attributes.Value,
                            Type = ScimConstants.DataTypes.String,
                            Description = "Identifier of the member.",
                            Mutability = ScimConstants.MutabilityValues.Immutable,
                            Returned = ScimConstants.ReturnedValues.Default
                        },
                        new ScimSchemaAttribute
                        {
                            Name = "$ref",
                            Type = ScimConstants.DataTypes.Reference,
                            Description = "The URI of the SCIM resource.",
                            Mutability = ScimConstants.MutabilityValues.Immutable,
                            Returned = ScimConstants.ReturnedValues.Default
                        },
                        new ScimSchemaAttribute
                        {
                            Name = "display",
                            Type = ScimConstants.DataTypes.String,
                            Description = "A human-readable name for the member.",
                            Mutability = ScimConstants.MutabilityValues.ReadOnly,
                            Returned = ScimConstants.ReturnedValues.Default
                        }
                    ]
                }
            ],
            Meta = new ScimMeta
            {
                ResourceType = ScimConstants.ResourceTypes.Schema,
                Location = $"{baseUrl}/{metadataRoute}/Schemas/{ScimConstants.GroupSchemaUrn}"
            }
        };
}
