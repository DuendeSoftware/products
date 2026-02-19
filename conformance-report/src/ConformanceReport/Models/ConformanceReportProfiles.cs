// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Container for conformance profile results.
/// </summary>
public sealed class ConformanceReportProfiles
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConformanceReportProfiles"/> class.
    /// </summary>
    internal ConformanceReportProfiles() { }

    /// <summary>
    /// OAuth 2.1 conformance assessment results.
    /// </summary>
    public ProfileResult? OAuth21 { get; internal init; }

    /// <summary>
    /// FAPI 2.0 Security Profile conformance assessment results.
    /// </summary>
    public ProfileResult? Fapi2Security { get; internal init; }
}
