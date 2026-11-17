// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class ServerSideSessionDso
{
    internal static readonly EntityType EntityType = new(2107, "ServerSideSessionDso");

    /// <summary>
    /// Converts a stored ServerSideSessionDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(ServerSideSessionDso)} type {dso.GetType()}.")
    };

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
