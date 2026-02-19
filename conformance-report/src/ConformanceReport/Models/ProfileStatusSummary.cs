// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Status summary for a specific conformance profile.
/// </summary>
public sealed class ProfileStatusSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileStatusSummary"/> class.
    /// </summary>
    internal ProfileStatusSummary() { }

    /// <summary>
    /// The number of clients that pass all requirements.
    /// </summary>
    public int Passing { get; internal init; }

    /// <summary>
    /// The number of clients that have warnings but no failures.
    /// </summary>
    public int Warning { get; internal init; }

    /// <summary>
    /// The number of clients that fail one or more requirements.
    /// </summary>
    public int Failing { get; internal init; }
}
