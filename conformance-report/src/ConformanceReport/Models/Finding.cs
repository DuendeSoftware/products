// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Represents a single conformance finding for a specific rule.
/// </summary>
public sealed class Finding
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Finding"/> class.
    /// </summary>
    internal Finding() { }

    /// <summary>
    /// The unique identifier for this rule (e.g., "S01", "C03").
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// A human-readable name for this rule.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// The status of this finding.
    /// </summary>
    public required FindingStatus Status { get; init; }

    /// <summary>
    /// A detailed message explaining the finding.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional recommendation for remediation when the status is Fail or Warning.
    /// </summary>
    public string? Recommendation { get; internal init; }
}
