// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Tracks the state of a SAML logout session, including which SPs are expected
/// to respond and which have already responded.
/// </summary>
public sealed class SamlLogoutSession
{
    /// <summary>
    /// The logout ID that correlates this session to the <c>LogoutMessage</c>.
    /// </summary>
    public required string LogoutId { get; init; }

    /// <summary>
    /// Expected SP logout responses, keyed by the LogoutRequest ID sent to each SP.
    /// </summary>
    public required Dictionary<string, ExpectedSpLogout> ExpectedResponses { get; init; }

    /// <summary>
    /// The number of SPs that could not be notified (disabled, no SLO URL,
    /// unsupported binding, or request generation failure). When greater than zero,
    /// the best achievable outcome is PartialLogout.
    /// </summary>
    public int SkippedSpCount { get; init; }

    /// <summary>
    /// When this session was created (UTC).
    /// </summary>
    public required DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// Gets or sets the UTC time at which this session expires.
    /// After this time, the session is considered invalid and may be cleaned up.
    /// </summary>
    public required DateTime ExpiresAtUtc { get; init; }
}

/// <summary>
/// Tracks an expected logout response from a specific SP.
/// </summary>
/// <param name="SpEntityId">The entity ID of the SP that should respond.</param>
/// <param name="Response">The response received, or <see langword="null"/> if still pending.</param>
public sealed record ExpectedSpLogout(string SpEntityId, SamlSpLogoutResponse? Response = null);

/// <summary>
/// Records the outcome of a single SP's logout response.
/// </summary>
/// <param name="Success">Whether the SP reported successful logout.</param>
/// <param name="ReceivedUtc">When the response was received.</param>
public sealed record SamlSpLogoutResponse(bool Success, DateTimeOffset ReceivedUtc);
