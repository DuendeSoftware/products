// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Represents the overall conformance status for a report, profile, server, or client.
/// </summary>
public enum ConformanceReportStatus
{
    /// <summary>
    /// All requirements are satisfied.
    /// </summary>
    Pass,

    /// <summary>
    /// Some recommendations are not followed, but no requirements are violated.
    /// </summary>
    Warn,

    /// <summary>
    /// One or more requirements are not satisfied.
    /// </summary>
    Fail
}
