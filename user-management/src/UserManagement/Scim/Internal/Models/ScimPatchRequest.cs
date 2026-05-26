// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Models;

internal sealed record ScimPatchRequest
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[]? Schemas { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Operations)]
    public required IReadOnlyList<ScimPatchOperation> Operations { get; init; }
}

internal sealed record ScimPatchOperation
{
    [JsonPropertyName(ScimConstants.Attributes.Op)]
    public required string Op { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Path)]
    public string? Path { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Value)]
    public JsonElement? Value { get; init; }
}
