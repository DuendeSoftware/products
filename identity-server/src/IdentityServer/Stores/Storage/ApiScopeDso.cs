// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class ApiScopeDso
{
    internal static readonly EntityType EntityType = new(2112, "ApiScopeDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required bool Enabled { get; init; }
        public string? DisplayName { get; init; }
        public string? Description { get; init; }
        public required bool ShowInDiscoveryDocument { get; init; }
        public required bool Required { get; init; }
        public required bool Emphasize { get; init; }
        public required IReadOnlyList<string> UserClaims { get; init; }
        public IReadOnlyList<AttributeValueEntryDso>? ExtendedAttributeValues { get; init; }
        public IReadOnlyList<ApiResourceReferenceDso.V1> ReferencedByApiResources { get; init; } = [];
    }
}
