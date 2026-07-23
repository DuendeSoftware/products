// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using Duende.Storage.Internal;

namespace Duende.MultiSpace.Internal.Storage;

internal static class SpaceDso
{
    internal static readonly EntityType EntityType = new(200, "SpaceDso");

    internal sealed record V1(
        [property: JsonPropertyName("id")] Guid SpaceId,
        [property: JsonPropertyName("n")] string Name,
        [property: JsonPropertyName("e")] bool Enabled,
        [property: JsonPropertyName("p")] int PoolId,
        [property: JsonPropertyName("mp")] IReadOnlyList<MatchPatternV1> MatchPatterns,
        [property: JsonPropertyName("d")] bool IsDeleted = false) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }

    internal sealed record MatchPatternV1(
        [property: JsonPropertyName("o")] string? Origin,
        [property: JsonPropertyName("p")] string? Path);
}
