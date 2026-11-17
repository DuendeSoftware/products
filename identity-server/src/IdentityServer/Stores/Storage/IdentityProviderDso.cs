// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class IdentityProviderDso
{
    internal static readonly EntityType EntityType = new(2111, "IdentityProviderDso");

    /// <summary>
    /// Converts a stored IdentityProviderDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(IdentityProviderDso)} type {dso.GetType()}.")
    };

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required Guid Id { get; init; }
        public required string Scheme { get; init; }
        public string? DisplayName { get; init; }
        public required bool Enabled { get; init; }
        public required string Type { get; init; }
        public IReadOnlyList<AttributeValueDso.V1>? ExtendedAttributeValues { get; init; }
    }
}
