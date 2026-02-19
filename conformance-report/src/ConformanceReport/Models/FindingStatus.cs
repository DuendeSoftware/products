// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Represents the status of a conformance finding.
/// </summary>
public enum FindingStatus
{
    /// <summary>
    /// The requirement is satisfied.
    /// </summary>
    Pass,

    /// <summary>
    /// The requirement is not satisfied.
    /// </summary>
    Fail,

    /// <summary>
    /// A potential issue was detected that may affect conformance.
    /// </summary>
    Warning,

    /// <summary>
    /// The requirement is not applicable to this configuration.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// An error occurred while assessing this requirement.
    /// </summary>
    Error
}
