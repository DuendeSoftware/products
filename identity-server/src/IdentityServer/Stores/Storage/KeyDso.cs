// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class KeyDso
{
    internal static readonly EntityType EntityType = new(2108, "KeyDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required string Id { get; init; }
        public required string Use { get; init; }
        public required int Version { get; init; }
        public required long CreatedUtcTicks { get; init; }
        public required string Algorithm { get; init; }
        public required bool IsX509Certificate { get; init; }
        public required bool DataProtected { get; init; }
        public required string Data { get; init; }
    }
}
