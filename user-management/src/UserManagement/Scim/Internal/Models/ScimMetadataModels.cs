// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;

namespace Duende.UserManagement.Scim.Internal.Models;

// ──────────────────────────────────────────────────────────────────────────────
// ServiceProviderConfig (RFC 7643 §5, RFC 7644 §4)
// ──────────────────────────────────────────────────────────────────────────────

internal sealed record ScimServiceProviderConfig
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[] Schemas { get; init; } = [ScimConstants.ServiceProviderConfigSchemaUrn];

    [JsonPropertyName("documentationUri")]
    public string? DocumentationUri { get; init; }

    [JsonPropertyName("patch")]
    public required ScimSupported Patch { get; init; }

    [JsonPropertyName("bulk")]
    public required ScimBulkSupported Bulk { get; init; }

    [JsonPropertyName("filter")]
    public required ScimFilterSupported Filter { get; init; }

    [JsonPropertyName("changePassword")]
    public required ScimSupported ChangePassword { get; init; }

    [JsonPropertyName("sort")]
    public required ScimSupported Sort { get; init; }

    [JsonPropertyName("etag")]
    public required ScimSupported ETag { get; init; }

    [JsonPropertyName("authenticationSchemes")]
    public required IReadOnlyList<ScimAuthenticationScheme> AuthenticationSchemes { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Meta)]
    public ScimMeta? Meta { get; init; }
}

internal sealed record ScimSupported
{
    [JsonPropertyName(ScimConstants.Attributes.Supported)]
    public required bool Supported { get; init; }
}

internal sealed record ScimBulkSupported
{
    [JsonPropertyName(ScimConstants.Attributes.Supported)]
    public required bool Supported { get; init; }

    [JsonPropertyName("maxOperations")]
    public required int MaxOperations { get; init; }

    [JsonPropertyName("maxPayloadSize")]
    public required int MaxPayloadSize { get; init; }
}

internal sealed record ScimFilterSupported
{
    [JsonPropertyName(ScimConstants.Attributes.Supported)]
    public required bool Supported { get; init; }

    [JsonPropertyName("maxResults")]
    public required int MaxResults { get; init; }
}

internal sealed record ScimAuthenticationScheme
{
    [JsonPropertyName(ScimConstants.Attributes.Type)]
    public required string Type { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Name)]
    public required string Name { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Description)]
    public required string Description { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.SpecUri)]
    public string? SpecUri { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.DocumentationUri)]
    public string? DocumentationUri { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
// ResourceType (RFC 7643 §6)
// ──────────────────────────────────────────────────────────────────────────────

internal sealed record ScimResourceType
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[] Schemas { get; init; } = [ScimConstants.ResourceTypeSchemaUrn];

    [JsonPropertyName(ScimConstants.Attributes.Id)]
    public required string Id { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Name)]
    public required string Name { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Description)]
    public string? Description { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Endpoint)]
    public required string Endpoint { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Schema)]
    public required string Schema { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.SchemaExtensions)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ScimSchemaExtension>? SchemaExtensions { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Meta)]
    public ScimMeta? Meta { get; init; }
}

internal sealed record ScimSchemaExtension
{
    [JsonPropertyName(ScimConstants.Attributes.Schema)]
    public required string Schema { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Required)]
    public required bool Required { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
// Schema (RFC 7643 §7)
// ──────────────────────────────────────────────────────────────────────────────

internal sealed record ScimSchemaDefinition
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[] Schemas { get; init; } = [ScimConstants.SchemaSchemaUrn];

    [JsonPropertyName(ScimConstants.Attributes.Id)]
    public required string Id { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Name)]
    public string? Name { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Description)]
    public string? Description { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.AttributesList)]
    public required IReadOnlyList<ScimSchemaAttribute> Attributes { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Meta)]
    public ScimMeta? Meta { get; init; }
}

internal sealed record ScimSchemaAttribute
{
    [JsonPropertyName(ScimConstants.Attributes.Name)]
    public required string Name { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Type)]
    public required string Type { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.MultiValued)]
    public bool MultiValued { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Description)]
    public string? Description { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Required)]
    public bool Required { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.CaseExact)]
    public bool CaseExact { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Mutability)]
    public string Mutability { get; init; } = "readWrite";

    [JsonPropertyName(ScimConstants.Attributes.Returned)]
    public string Returned { get; init; } = "default";

    [JsonPropertyName(ScimConstants.Attributes.Uniqueness)]
    public string Uniqueness { get; init; } = "none";

    [JsonPropertyName(ScimConstants.Attributes.SubAttributes)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ScimSchemaAttribute>? SubAttributes { get; init; }
}

/// <summary>
/// Generic list response for metadata endpoints (ResourceTypes, Schemas).
/// Unlike <see cref="ScimListResponse{T}"/> this is not typed to a specific resource.
/// </summary>
internal sealed record ScimMetadataListResponse<T>
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[] Schemas { get; init; } = [ScimConstants.ListResponseSchemaUrn];

    [JsonPropertyName(ScimConstants.Attributes.TotalResults)]
    public required int TotalResults { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Resources)]
    public required IReadOnlyList<T> Resources { get; init; }
}
