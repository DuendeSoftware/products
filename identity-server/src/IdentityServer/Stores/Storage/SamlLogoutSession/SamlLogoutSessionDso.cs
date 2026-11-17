// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.SamlLogoutSession;

internal static class SamlLogoutSessionDso
{
    internal static readonly EntityType EntityType = new(2110, "SamlLogoutSessionDso");

    /// <summary>
    /// Converts a stored SamlLogoutSessionDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(SamlLogoutSessionDso)} type {dso.GetType()}.")
    };

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        /// <summary>Logout ID — stored for DSK reconstruction on Update.</summary>
        public required string LogoutId { get; init; }

        /// <summary>Request IDs — stored for DSK reconstruction on Update.</summary>
        public required IReadOnlyList<string> RequestIds { get; init; }

        /// <summary>JSON-serialized SamlLogoutSession.</summary>
        public required string SerializedSession { get; init; }

        /// <summary>Expiration as UTC ticks for in-store expiration checks.</summary>
        public required long ExpiresAtUtcTicks { get; init; }
    }
}
