// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class ClientDso
{
    internal static readonly EntityType EntityType = new(2100, "ClientDso");

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        // Identity
        public required Guid Id { get; init; }
        public required string ClientId { get; init; }
        public required bool Enabled { get; init; }
        public required string ProtocolType { get; init; }

        // Display
        public string? ClientName { get; init; }
        public string? Description { get; init; }
        public string? ClientUri { get; init; }
        public string? LogoUri { get; init; }

        // Authentication
        public required bool RequireClientSecret { get; init; }
        public required bool RequirePkce { get; init; }
        public required bool AllowPlainTextPkce { get; init; }
        public required bool RequireRequestObject { get; init; }
        public required bool RequireDPoP { get; init; }
        public required int DPoPValidationMode { get; init; }
        public required long DPoPClockSkewTicks { get; init; }

        // Consent
        public required bool RequireConsent { get; init; }
        public required bool AllowRememberConsent { get; init; }
        public int? ConsentLifetime { get; init; }

        // Tokens
        public required bool AllowAccessTokensViaBrowser { get; init; }
        public required bool AllowOfflineAccess { get; init; }
        public required int AccessTokenType { get; init; }
        public required bool IncludeJwtId { get; init; }
        public required int IdentityTokenLifetime { get; init; }
        public required int AccessTokenLifetime { get; init; }
        public required int AuthorizationCodeLifetime { get; init; }

        // Refresh
        public required int AbsoluteRefreshTokenLifetime { get; init; }
        public required int SlidingRefreshTokenLifetime { get; init; }
        public required int RefreshTokenUsage { get; init; }
        public required int RefreshTokenExpiration { get; init; }
        public required bool UpdateAccessTokenClaimsOnRefresh { get; init; }

        // Claims
        public required bool AlwaysIncludeUserClaimsInIdToken { get; init; }
        public required bool AlwaysSendClientClaims { get; init; }
        public string? ClientClaimsPrefix { get; init; }
        public string? PairWiseSubjectSalt { get; init; }

        // Session
        public int? UserSsoLifetime { get; init; }
        public bool? CoordinateLifetimeWithUserSession { get; init; }

        // Login/Logout
        public required bool EnableLocalLogin { get; init; }
        public string? FrontChannelLogoutUri { get; init; }
        public required bool FrontChannelLogoutSessionRequired { get; init; }
        public string? BackChannelLogoutUri { get; init; }
        public required bool BackChannelLogoutSessionRequired { get; init; }
        public string? InitiateLoginUri { get; init; }

        // PAR
        public required bool RequirePushedAuthorization { get; init; }
        public int? PushedAuthorizationLifetime { get; init; }

        // Device/CIBA
        public string? UserCodeType { get; init; }
        public required int DeviceCodeLifetime { get; init; }
        public int? CibaLifetime { get; init; }
        public int? PollingInterval { get; init; }

        // Collections
        public required IReadOnlyList<string> AllowedGrantTypes { get; init; }
        public required IReadOnlyList<string> AllowedScopes { get; init; }
        public required IReadOnlyList<string> RedirectUris { get; init; }
        public required IReadOnlyList<string> PostLogoutRedirectUris { get; init; }
        public required IReadOnlyList<string> AllowedIdentityTokenSigningAlgorithms { get; init; }
        public required IReadOnlyList<string> IdentityProviderRestrictions { get; init; }
        public required IReadOnlyList<string> AllowedCorsOrigins { get; init; }
        public required IReadOnlyList<SecretDso> ClientSecrets { get; init; }
        public required IReadOnlyList<ClaimDso> Claims { get; init; }
        /// <summary>
        ///     Extended attribute values for the client.
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

    internal sealed record ClaimDso(
        string Type,
        string Value,
        string ValueType);
}
