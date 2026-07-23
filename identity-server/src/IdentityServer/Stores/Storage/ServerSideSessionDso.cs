// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class ServerSideSessionDso
{
    internal static readonly EntityType EntityType = new(2107, "ServerSideSessionDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required string Key { get; init; }
        public required string Scheme { get; init; }
        public required string SubjectId { get; init; }
        public required string SessionId { get; init; }
        public string? DisplayName { get; init; }
        public required long CreatedUtcTicks { get; init; }
        public required long RenewedUtcTicks { get; init; }
        public long? ExpiresUtcTicks { get; init; }
        public required string Ticket { get; init; }
    }
}
