// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Models;

internal sealed record ScimSearchRequest
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[]? Schemas { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Filter)]
    public string? Filter { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.SortBy)]
    public string? SortBy { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.SortOrder)]
    public string? SortOrder { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.StartIndex)]
    public int? StartIndex { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Count)]
    public int? Count { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.AttributesParam)]
    public string[]? Attributes { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.ExcludedAttributes)]
    public string[]? ExcludedAttributes { get; init; }
}
