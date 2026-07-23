// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Infrastructure;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.Interaction.Scenarios;

/// <summary>
/// Represents an inline WebApplication hosted as a custom Aspire resource.
/// </summary>
public sealed class InlineWebAppResource(string name, BuildWebApp factory)
    : Resource(name), IResourceWithEndpoints
{
    internal const string HttpEndpointName = "http";

    private EndpointReference? _httpEndpoint;

    /// <summary>The factory used to build the inline WebApplication.</summary>
    public BuildWebApp Factory { get; } = factory;

    /// <summary>The running WebApplication instance; set after startup.</summary>
    public WebApplication? App { get; internal set; }

    /// <summary>Reference to the HTTP endpoint exposed by this resource.</summary>
    public EndpointReference HttpEndpoint =>
        _httpEndpoint ??= new EndpointReference(this, HttpEndpointName);
}
