// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.DevPortal;

namespace Duende.IdentityServer.Interaction.Infrastructure;

/// <summary>
/// An Aspire resource that wraps an <see cref="IScenario"/>, providing Start/Stop
/// lifecycle management via the Aspire dashboard.
/// </summary>
public sealed class ScenarioResource(IScenario scenario)
    : Resource(scenario.Name), IResourceWithEndpoints
{
    internal const string HttpEndpointName = "http";

    private EndpointReference? _httpEndpoint;

    /// <summary>The scenario implementation.</summary>
    public IScenario Scenario { get; } = scenario;

    /// <summary>Whether the scenario is currently running.</summary>
    public bool IsRunning { get; internal set; }

    /// <summary>The DevPortal host created by the infrastructure after the scenario starts.</summary>
    public DevPortalTestHost? DevPortal { get; internal set; }

    /// <summary>Reference to the primary HTTP endpoint.</summary>
    public EndpointReference HttpEndpoint =>
        _httpEndpoint ??= new EndpointReference(this, HttpEndpointName);
}
