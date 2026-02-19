// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport;

/// <summary>
/// Represents a client for conformance assessment.
/// </summary>
internal sealed record ConformanceReportClient
{
    public required string ClientId { get; init; }

    public string? ClientName { get; init; }

    public required IReadOnlyCollection<string> AllowedGrantTypes { get; init; }

    public required bool RequirePkce { get; init; }

    public required bool AllowPlainTextPkce { get; init; }

    public required IReadOnlyCollection<string> RedirectUris { get; init; }

    public required bool RequireClientSecret { get; init; }

    public required IReadOnlyCollection<string> ClientSecretTypes { get; init; }

    public required bool RequirePushedAuthorization { get; init; }

    public required bool RequireDPoP { get; init; }

    public required ConformanceReportDPoPValidationMode DPoPValidationMode { get; init; }

    public required int AuthorizationCodeLifetime { get; init; }

    public required bool AllowOfflineAccess { get; init; }

    public required ConformanceReportTokenUsage RefreshTokenUsage { get; init; }

    public required bool AllowAccessTokensViaBrowser { get; init; }

    public required bool RequireRequestObject { get; init; }
}
