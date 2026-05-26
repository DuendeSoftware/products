// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Models;

internal sealed record ScimBulkResponse
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[] Schemas { get; init; } = [ScimConstants.BulkResponseSchemaUrn];

    [JsonPropertyName(ScimConstants.Attributes.Operations)]
    public required IReadOnlyList<ScimBulkOperationResponse> Operations { get; init; }
}

internal sealed record ScimBulkOperationResponse
{
    [JsonPropertyName(ScimConstants.Attributes.Method)]
    public required string Method { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.BulkId)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BulkId { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Version)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Location)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Status)]
    public required string Status { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Response)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Response { get; init; }
}
