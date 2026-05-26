// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

/// <summary>Represents a SCIM Group resource in API responses.</summary>
internal sealed record ScimGroupResource
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public required string[] Schemas { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Id)]
    public required string Id { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.DisplayName)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? DisplayName { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Members)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ScimGroupMember>? Members { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Meta)]
    public required Endpoints.Users.ScimMeta Meta { get; init; }
}

/// <summary>Represents a single member entry in a SCIM Group resource.</summary>
internal sealed record ScimGroupMember
{
    /// <summary>The subject ID of the member (user or group).</summary>
    [JsonPropertyName(ScimConstants.Attributes.Value)]
    public required string Value { get; init; }

    /// <summary>Display name of the member (e.g., username).</summary>
    [JsonPropertyName(ScimConstants.Attributes.Display)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Display { get; init; }

    /// <summary>URI reference to the member resource.</summary>
    [JsonPropertyName(ScimConstants.Attributes.Ref)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ref { get; init; }

    /// <summary>Resource type of the member; always "User" for group members.</summary>
    [JsonPropertyName(ScimConstants.Attributes.Type)]
    public string Type { get; init; } = ScimConstants.ResourceTypes.User;
}
