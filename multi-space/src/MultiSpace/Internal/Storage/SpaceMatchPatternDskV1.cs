// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.MultiSpace.Internal.Storage;

internal sealed record SpaceMatchPatternDskV1 : IDataStorageKey
{
    private SpaceMatchPatternDskV1(string? origin, string? path)
    {
        Origin = origin;
        Path = path;
    }

    public static DataStorageKeyVersion DskVersion { get; } =
        new(new DataStorageKeyType(200_001u, "SpaceMatchPattern"), 1);

    public string? Origin { get; }
    public string? Path { get; }

#pragma warning disable CA1308 // Normalize strings to uppercase — lowercase is appropriate for case-insensitive URL matching
    public static SpaceMatchPatternDskV1 Create(string? origin, string? path) =>
        new(origin?.ToLowerInvariant(), path?.ToLowerInvariant());
#pragma warning restore CA1308
}
