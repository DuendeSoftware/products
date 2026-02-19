// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport;

/// <summary>
/// Represents license information for the conformance report.
/// </summary>
public sealed record ConformanceReportLicenseInfo
{
    /// <summary>
    /// The company name from the license.
    /// </summary>
    public string? CompanyName { get; init; }

    /// <summary>
    /// The contact information from the license.
    /// </summary>
    public string? ContactInfo { get; init; }

    /// <summary>
    /// The license serial number.
    /// </summary>
    public int? SerialNumber { get; init; }

    /// <summary>
    /// The license expiration date.
    /// </summary>
    public DateTime? Expiration { get; init; }

    /// <summary>
    /// The license edition name.
    /// </summary>
    public string? Edition { get; init; }
}
