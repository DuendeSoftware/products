// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Models;

internal sealed record ScimBulkRequest
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[]? Schemas { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.FailOnErrors)]
    public int? FailOnErrors { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Operations)]
    public required IReadOnlyList<ScimBulkOperation> Operations { get; init; }
}

internal sealed record ScimBulkOperation
{
    [JsonPropertyName(ScimConstants.Attributes.Method)]
    public required string Method { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.BulkId)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BulkId { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Version)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Path)]
    public required string Path { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Data)]
    public JsonElement? Data { get; init; }
}
