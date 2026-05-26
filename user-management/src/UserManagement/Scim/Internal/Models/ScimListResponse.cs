// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Scim.Internal.Models;

internal sealed record ScimListResponse<T>
{
    [JsonPropertyName(ScimConstants.Attributes.Schemas)]
    public string[] Schemas { get; init; } = [ScimConstants.ListResponseSchemaUrn];

    [JsonPropertyName(ScimConstants.Attributes.TotalResults)]
    public required int TotalResults { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.StartIndex)]
    public required int StartIndex { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.ItemsPerPage)]
    public required int ItemsPerPage { get; init; }

    [JsonPropertyName(ScimConstants.Attributes.Resources)]
    public required IReadOnlyList<T> Resources { get; init; }
}
