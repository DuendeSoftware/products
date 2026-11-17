// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class SamlSigninStateDso
{
    internal static readonly EntityType EntityType = new(2109, "SamlSigninStateDso");

    /// <summary>
    /// Converts a stored SamlSigninStateDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(SamlSigninStateDso)} type {dso.GetType()}.")
    };

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required string SerializedState { get; init; }
        public required long ExpiresAtUtcTicks { get; init; }
        public required string ServiceProviderEntityId { get; init; }
    }
}
