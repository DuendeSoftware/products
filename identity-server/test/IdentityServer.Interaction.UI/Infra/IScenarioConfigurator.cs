// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.UI.Infra;

/// <summary>
/// A named link exposed by a running scenario (shown as clickable URLs in the Aspire dashboard).
/// </summary>
public sealed record ScenarioLink(string Label, Uri Url);

/// <summary>
/// Provides pre-configured WebApplicationBuilder instances to scenarios.
/// Aspire provides one with OTel + resource logging; tests provide a plain one.
/// </summary>
public interface IScenarioConfigurator
{
    /// <summary>
    /// Creates a <see cref="WebApplicationBuilder"/> pre-configured with logging
    /// (and optionally telemetry).
    /// </summary>
    /// <param name="serviceName">
    /// Service name for telemetry (e.g., "par-web-client-is").
    /// </param>
    WebApplicationBuilder CreateBuilder(string serviceName);
}
