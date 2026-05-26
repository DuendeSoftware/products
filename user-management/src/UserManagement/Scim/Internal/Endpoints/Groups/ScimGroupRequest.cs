// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

/// <summary>Represents a SCIM Group resource in create/replace request bodies.</summary>
internal sealed record ScimGroupRequest
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[]? Schemas { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.DisplayName)]
    public string? DisplayName { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Members)]
    public IReadOnlyList<ScimGroupMemberRequest>? Members { get; init; }
}

/// <summary>Represents a member entry in a SCIM Group create/replace request.</summary>
internal sealed record ScimGroupMemberRequest
{
    /// <summary>The subject ID (value) of the member to add.</summary>
    [JsonPropertyName(ScimConstants.Attributes.Value)]
    public string? Value { get; init; }
}
