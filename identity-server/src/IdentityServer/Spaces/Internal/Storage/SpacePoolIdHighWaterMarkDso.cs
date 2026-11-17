// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json.Serialization;
using Duende.Storage;
using Duende.Storage.Internal;

namespace Duende.Spaces.Internal.Storage;

internal static class SpacePoolIdHighWaterMarkDso
{
    internal static readonly EntityType EntityType = new(203, nameof(SpacePoolIdHighWaterMarkDso));
    internal static readonly UuidV7 Id = UuidV7.From(Guid.Parse("00000000-0000-7000-8000-000000000001"));

    /// <summary>
    /// Converts a stored SpacePoolIdHighWaterMarkDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(SpacePoolIdHighWaterMarkDso)} type {dso.GetType()}.")
    };

    internal sealed record V1(
        [property: JsonPropertyName("m")] int MaxPoolId) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
