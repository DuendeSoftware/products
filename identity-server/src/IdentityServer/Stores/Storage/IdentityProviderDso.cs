// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class IdentityProviderDso
{
    internal static readonly EntityType EntityType = new(2111, "IdentityProviderDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required Guid Id { get; init; }
        public required string Scheme { get; init; }
        public string? DisplayName { get; init; }
        public required bool Enabled { get; init; }
        public required string Type { get; init; }
        public required IReadOnlyDictionary<string, string> Properties { get; init; }
    }
}
