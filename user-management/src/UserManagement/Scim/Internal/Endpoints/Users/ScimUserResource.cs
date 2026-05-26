// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

/// <summary>Represents a SCIM User resource in API responses.</summary>
internal sealed record ScimUserResource
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public required string[] Schemas { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Id)]
    public required string Id { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.ExternalId)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExternalId { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.UserName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserName { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Meta)]
    public required ScimMeta Meta { get; init; }

    // Dynamic attributes as additional key-value pairs
    [JsonExtensionData]
    public Dictionary<string, object?>? AdditionalAttributes { get; init; }
}

internal sealed record ScimMeta
{
    [JsonPropertyName(ScimConstants.Attributes.ResourceType)]
    public required string ResourceType { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Location)]
    public required string Location { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Version)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }
}
