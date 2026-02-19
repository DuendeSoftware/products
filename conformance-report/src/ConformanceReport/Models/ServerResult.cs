// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Represents the server-level conformance assessment results.
/// </summary>
public sealed class ServerResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerResult"/> class.
    /// </summary>
    internal ServerResult() { }

    /// <summary>
    /// The overall status of server-level conformance.
    /// </summary>
    public required ConformanceReportStatus Status { get; init; }

    /// <summary>
    /// The list of server-level conformance findings.
    /// </summary>
    public required IReadOnlyList<Finding> Findings { get; init; }
}
