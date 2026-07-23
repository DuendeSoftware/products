// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.Interaction.Infrastructure;

/// <summary>
/// Defines a test scenario that hosts one or more WebApplications in-process.
/// Each scenario groups related services (IdentityServer host, client app, API, etc.)
/// and manages their lifecycle as a unit.
/// </summary>
public interface IScenario
{
    /// <summary>Unique identifier for the scenario (e.g., "par-web-client").</summary>
    string Name { get; }

    /// <summary>Human-readable description shown in the Aspire dashboard.</summary>
    string Description { get; }

    /// <summary>
    /// URLs exposed when the scenario is running. Shown as clickable links in the dashboard.
    /// Only meaningful after <see cref="StartAsync"/> completes.
    /// </summary>
    IReadOnlyList<ScenarioLink> Links { get; }

    /// <summary>
    /// Starts all WebApplication instances for this scenario.
    /// </summary>
    /// <param name="configurator">
    /// Provides pre-configured <see cref="WebApplicationBuilder"/> instances with
    /// logging (and optionally OTel) already wired.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct);

    /// <summary>
    /// Stops and disposes all WebApplication instances for this scenario.
    /// </summary>
    Task StopAsync(CancellationToken ct);

    Command[] GetCommands();
}

public record Command
{
    public required string Name { get; init; }
    public string Icon { get; init; } = "DocumentLightning";

    public required Func<CommandContext, Task<ExecuteCommandResult>> Execute { get; init; }

}
