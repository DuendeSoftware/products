// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class PushedAuthorizationDso
{
    internal static readonly EntityType EntityType = new(2106, "PushedAuthorizationDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required string ReferenceValueHash { get; init; }
        public required string Parameters { get; init; }
        public required long ExpiresAtUtcTicks { get; init; }
    }
}
