// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class ApiResourceDso
{
    internal static readonly EntityType EntityType = new(2102, "ApiResourceDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required bool Enabled { get; init; }
        public string? DisplayName { get; init; }
        public string? Description { get; init; }
        public required bool ShowInDiscoveryDocument { get; init; }
        public required bool RequireResourceIndicator { get; init; }
        public required IReadOnlyList<string> UserClaims { get; init; }
        public required IReadOnlyList<ApiScopeReferenceDso.V1> Scopes { get; init; }
        public required IReadOnlyList<string> AllowedAccessTokenSigningAlgorithms { get; init; }
        public required IReadOnlyList<SecretDso> ApiSecrets { get; init; }

        /// <summary>
        ///     Extended attribute values for the API resource.
        /// </summary>
        public IReadOnlyList<AttributeValueEntryDso>? ExtendedAttributeValues { get; init; }
    }

    internal sealed record SecretDso(
        Guid Id,
        string Value,
        string? Description,
        DateTime? Expiration,
        string Type,
        string HashAlgorithm);
}
