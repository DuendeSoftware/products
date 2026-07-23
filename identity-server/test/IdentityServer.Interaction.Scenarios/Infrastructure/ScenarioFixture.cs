// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.UI.Infra;

namespace Duende.IdentityServer.Interaction.Tests.Infrastructure;

/// <summary>
/// Base fixture for scenario-based tests. Probes the scenario's first link to determine
/// if it's already running (e.g., started via the Aspire dashboard). If not, starts it
/// using <see cref="TestScenarioConfigurator"/>.
/// </summary>
public sealed class ScenarioFixture<T> : IAsyncLifetime
    where T : IScenario, new()
{
    private readonly IScenario _scenario = new T();
    private readonly ProxyOutput _output = new();
    private bool _ownedByFixture;

    /// <summary>The scenario instance.</summary>
    public IScenario Scenario => _scenario;

    /// <summary>The scenario's links (URLs available when running).</summary>
    public IReadOnlyList<ScenarioLink> Links => _scenario.Links;
    public Uri Link(string label) => _scenario.Links.FirstOrDefault(x => x.Label == label)?.Url
                                      ?? throw new InvalidOperationException($"Link with label '{label}' not found.");

    /// <summary>Connect the current test's output helper.</summary>
    public void Attach(ITestOutputHelper output) => _output.Attach(output);

    /// <summary>Disconnect the current test's output helper, flushing startup logs.</summary>
    public void Detach() => _output.Detach();

    public async ValueTask InitializeAsync()
    {
        // Probe the first link to see if the scenario is already running
        if (_scenario.Links.Count > 0)
        {
            var isRunning = await ProbeEndpointAsync(_scenario.Links[0].Url);
            if (isRunning)
            {
                // Scenario already started (e.g., by Aspire dashboard) — reuse it
                return;
            }
        }

        // Not running — start it ourselves
        var configurator = new TestScenarioConfigurator(_output);
        await _scenario.StartAsync(configurator, CancellationToken.None);
        _ownedByFixture = true;
    }

    public async ValueTask DisposeAsync()
    {
        // Only stop if we started it
        if (_ownedByFixture)
        {
            await _scenario.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<bool> ProbeEndpointAsync(Uri url)
    {
        try
        {
            using var client = new HttpClient()
            {
                Timeout = TimeSpan.FromMilliseconds(500)
            };
            var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
