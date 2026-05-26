// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Models;

internal sealed record ScimErrorResponse
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[] Schemas { get; init; } = [ScimConstants.ErrorSchemaUrn];

    [JsonPropertyName(ScimConstants.Attributes.Status)]
    public required string Status { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.ScimType)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScimType { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Detail)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }
}
