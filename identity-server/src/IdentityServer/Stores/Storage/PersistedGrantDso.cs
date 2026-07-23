// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class PersistedGrantDso
{
    internal static readonly EntityType EntityType = new(2103, "PersistedGrantDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        // Identity
        public required Guid Id { get; init; }
        public required string Key { get; init; }

        // Grant properties
        public required string Type { get; init; }
        public string? SubjectId { get; init; } // Nullable — client credentials flows have no subject
        public string? SessionId { get; init; }
        public required string ClientId { get; init; }
        public string? Description { get; init; }

        // Temporal (stored as UTC ticks)
        public required long CreationTimeTicks { get; init; }
        public long? ExpirationTicks { get; init; }
        public long? ConsumedTimeTicks { get; init; }

        // Payload
        public required string Data { get; init; }
    }
}
