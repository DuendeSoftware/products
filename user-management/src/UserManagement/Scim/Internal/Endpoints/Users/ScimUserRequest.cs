// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

internal sealed record ScimUserRequest
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[]? Schemas { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.ExternalId)]
    public string? ExternalId { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.UserName)]
    public string? UserName { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Password)]
    public string? Password { get; init; }

    // Dynamic attributes
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalAttributes { get; init; }
}
