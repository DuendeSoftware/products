// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Represents the conformance assessment results for a specific profile.
/// </summary>
public sealed class ProfileResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileResult"/> class.
    /// </summary>
    internal ProfileResult() { }

    /// <summary>
    /// The display name for this profile (e.g., "OAuth 2.1", "FAPI 2.0 Security Profile").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The specification version assessed (e.g., "draft-14", "1.0").
    /// </summary>
    public required string SpecVersion { get; init; }

    /// <summary>
    /// The specification status (e.g., "draft", "final").
    /// </summary>
    public required string SpecStatus { get; init; }

    /// <summary>
    /// Optional note about the profile (e.g., draft specification warning).
    /// </summary>
    public string? Note { get; internal init; }

    /// <summary>
    /// The overall conformance status for this profile.
    /// </summary>
    public required ConformanceReportStatus Status { get; init; }

    /// <summary>
    /// Server-level conformance results.
    /// </summary>
    public required ServerResult Server { get; init; }

    /// <summary>
    /// Per-client conformance results.
    /// </summary>
    public required IReadOnlyList<ClientResult> Clients { get; init; }

    /// <summary>
    /// Summary statistics for this profile.
    /// </summary>
    public required ProfileSummary Summary { get; init; }
}
