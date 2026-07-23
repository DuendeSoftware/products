// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class SamlSigninStateDso
{
    internal static readonly EntityType EntityType = new(2109, "SamlSigninStateDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required string SerializedState { get; init; }
        public required long ExpiresAtUtcTicks { get; init; }
        public required string ServiceProviderEntityId { get; init; }
    }
}
