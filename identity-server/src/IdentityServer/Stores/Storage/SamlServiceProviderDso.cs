// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class SamlServiceProviderDso
{
    internal static readonly EntityType EntityType = new(2101, "SamlServiceProviderDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        // Identity
        public required Guid Id { get; init; }
        public required string EntityId { get; init; }
        public required bool Enabled { get; init; }

        // Display
        public string? DisplayName { get; init; }
        public string? Description { get; init; }

        // Timing
        public long? ClockSkewTicks { get; init; }
        public long? RequestMaxAgeTicks { get; init; }
        public long? AssertionLifetimeTicks { get; init; }

        // Endpoints
        public required IReadOnlyList<IndexedEndpointDso> AssertionConsumerServiceUrls { get; init; }
        public required IReadOnlyList<EndpointDso> SingleLogoutServiceUrls { get; init; }

        // Security
        public bool? RequireSignedAuthnRequests { get; init; }
        public bool? RequireSignedLogoutResponses { get; init; }
        public required IReadOnlyList<CertificateDso> Certificates { get; init; }

        // SSO
        public required bool AllowIdpInitiated { get; init; }

        // Scopes
        public required IReadOnlyList<string> AllowedScopes { get; init; }

        // Claims
        public required IReadOnlyDictionary<string, string> ClaimMappings { get; init; }
        public required IReadOnlyDictionary<string, string> AuthnContextMappings { get; init; }
        public required IReadOnlyList<string> RequestedClaimTypes { get; init; }

        // NameID
        public string? DefaultNameIdFormat { get; init; }
        public string? EmailNameIdClaimType { get; init; }

        // Signing
        public int? SigningBehavior { get; init; }
        public required IReadOnlyList<string> AllowedSignatureAlgorithms { get; init; }
    }

    internal sealed record IndexedEndpointDso(string Location, int Binding, int Index, bool IsDefault);

    internal sealed record EndpointDso(string Location, int Binding);

    internal sealed record CertificateDso(Guid Id, string Base64Data, int Use);
}
