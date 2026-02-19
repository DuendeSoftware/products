// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Models;

/// <summary>
/// Represents the conformance assessment results for a single client.
/// </summary>
public sealed class ClientResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientResult"/> class.
    /// </summary>
    internal ClientResult() { }

    public required string ClientId { get; init; }

    /// <summary>
    /// The client name, if available.
    /// </summary>
    public string? ClientName { get; internal init; }

    /// <summary>
    /// The overall conformance status for this client.
    /// </summary>
    public required ConformanceReportStatus Status { get; init; }

    /// <summary>
    /// The list of findings for this client.
    /// </summary>
    public required IReadOnlyList<Finding> Findings { get; init; }
}
