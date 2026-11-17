// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class SamlServiceProviderDso
{
    internal static readonly EntityType EntityType = new(2101, "SamlServiceProviderDso");

    /// <summary>
    /// Converts a stored SamlServiceProviderDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(SamlServiceProviderDso)} type {dso.GetType()}.")
    };

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

        // Extended properties
        public IReadOnlyList<AttributeValueDso.V1>? ExtendedAttributeValues { get; init; }
    }

    internal sealed record IndexedEndpointDso(string Location, int Binding, int Index, bool IsDefault);

    internal sealed record EndpointDso(string Location, int Binding);

    internal sealed record CertificateDso(Guid Id, string Base64Data, int Use);
}
