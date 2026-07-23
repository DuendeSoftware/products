// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class DeviceFlowDso
{
    internal static readonly EntityType EntityType = new(2105, "DeviceFlowDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        // Key strings stored redundantly so UpdateAsync can reconstruct both DSKs.
        // Values are SHA-256 hashes (pre-hashed by DefaultDeviceFlowCodeService).
        public required string DeviceCode { get; init; }
        public required string UserCode { get; init; }

        // Serialized DeviceCode model (via IPersistentGrantSerializer)
        public required string Data { get; init; }
    }
}
