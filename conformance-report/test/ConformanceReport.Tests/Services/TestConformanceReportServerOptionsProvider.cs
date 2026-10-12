// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Test double that returns the current value of <see cref="Options"/>
/// and records how it was called.
/// </summary>
internal sealed class TestConformanceReportServerOptionsProvider(ConformanceReportServerOptions options)
    : IConformanceReportServerOptionsProvider
{
    /// <summary>
    /// Gets or sets the server options returned by <see cref="GetServerOptionsAsync"/>.
    /// Set it between calls to simulate a configuration change.
    /// </summary>
    public ConformanceReportServerOptions Options { get; set; } = options;

    /// <summary>
    /// Gets the number of times <see cref="GetServerOptionsAsync"/> was called.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Gets the cancellation token passed to the most recent call.
    /// </summary>
    public Ct LastCancellationToken { get; private set; }

    /// <inheritdoc />
    public Task<ConformanceReportServerOptions> GetServerOptionsAsync(Ct ct)
    {
        CallCount++;
        LastCancellationToken = ct;
        return Task.FromResult(Options);
    }
}
