// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Overall summary statistics for the conformance report.
/// </summary>
public sealed class OverallSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OverallSummary"/> class.
    /// </summary>
    internal OverallSummary() { }

    /// <summary>
    /// The total number of clients assessed.
    /// </summary>
    public int TotalClients { get; internal init; }

    /// <summary>
    /// Summary statistics for OAuth 2.1 profile.
    /// </summary>
    public required ProfileStatusSummary OAuth21 { get; init; }

    /// <summary>
    /// Summary statistics for FAPI 2.0 Security Profile.
    /// </summary>
    public required ProfileStatusSummary Fapi2Security { get; init; }
}
