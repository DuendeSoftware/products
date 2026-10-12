// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport;

/// <summary>
/// Supplies the server configuration that the conformance assessment evaluates.
/// </summary>
/// <remarks>
/// The provider is called once per report, so implementations can return current
/// configuration (for example, configuration loaded from a store) and do not need to cache it.
/// </remarks>
internal interface IConformanceReportServerOptionsProvider
{
    /// <summary>
    /// Gets the server options to assess.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The server options for the current assessment.</returns>
    Task<ConformanceReportServerOptions> GetServerOptionsAsync(Ct ct);
}
