// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Summary statistics for a conformance profile assessment.
/// </summary>
public sealed class ProfileSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileSummary"/> class.
    /// </summary>
    internal ProfileSummary() { }

    /// <summary>
    /// The total number of clients assessed.
    /// </summary>
    public int TotalClients { get; internal init; }

    /// <summary>
    /// The number of clients that pass all requirements.
    /// </summary>
    public int PassingClients { get; internal init; }

    /// <summary>
    /// The number of clients that have warnings but no failures.
    /// </summary>
    public int WarningClients { get; internal init; }

    /// <summary>
    /// The number of clients that fail one or more requirements.
    /// </summary>
    public int FailingClients { get; internal init; }
}
