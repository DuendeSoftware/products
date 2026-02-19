// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Represents a complete conformance assessment report.
/// </summary>
public sealed class ConformanceReportResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConformanceReportResult"/> class.
    /// </summary>
    internal ConformanceReportResult() { }

    /// <summary>
    /// The unique identifier for this report type (for GRC tool integration).
    /// </summary>
    public string Id { get; internal init; } = ConformanceReportConstants.ReportId;

    /// <summary>
    /// The display name for this report (for GRC tool integration).
    /// </summary>
    public string Name { get; internal init; } = ConformanceReportConstants.ReportName;

    /// <summary>
    /// The license information (if available).
    /// </summary>
    public ConformanceReportLicenseInfo? License { get; init; }

    /// <summary>
    /// The version of the Conformance Report tool.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The absolute URL to the HTML report endpoint.
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// The overall conformance status across all profiles.
    /// </summary>
    public required ConformanceReportStatus Status { get; init; }

    /// <summary>
    /// The timestamp when this report was generated.
    /// </summary>
    public required DateTimeOffset AssessedAt { get; init; }

    /// <summary>
    /// The results for each conformance profile.
    /// </summary>
    public required ConformanceReportProfiles Profiles { get; init; }

    /// <summary>
    /// Overall summary statistics across all profiles.
    /// </summary>
    public required OverallSummary OverallSummary { get; init; }

    /// <summary>
    /// The name of the hosting company (optional).
    /// </summary>
    public string? HostCompanyName { get; init; }

    /// <summary>
    /// The URL to the hosting company's logo (optional).
    /// </summary>
    public Uri? HostCompanyLogoUrl { get; init; }
}
