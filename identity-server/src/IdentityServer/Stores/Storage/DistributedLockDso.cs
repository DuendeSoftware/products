// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class DistributedLockDso
{
    internal static readonly EntityType EntityType = new(2113, "DistributedLockDso");

    /// <summary>
    /// Converts a stored DistributedLockDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(DistributedLockDso)} type {dso.GetType()}.")
    };

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required string Name { get; init; }
        public Guid? HolderId { get; init; }
        public string? HolderMachine { get; init; }
        public required DateTimeOffset ExpiresAtUtc { get; init; }
    }
}
