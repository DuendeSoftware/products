// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Duende.UserManagement.TestIsolation;

/// <summary>
/// Bridges <see cref="IApplicationBuilder"/> and <see cref="IEndpointRouteBuilder"/>
/// so test pipeline configuration can use both middleware and endpoint routing APIs
/// (e.g. <c>app.MapGet(...)</c>) on a branched builder.
/// </summary>
public sealed class WebAppWrapper : IApplicationBuilder, IEndpointRouteBuilder
{
    private readonly IApplicationBuilder _builder;
    private IServiceProvider _services;
    private readonly IEndpointRouteBuilder _endpoints;

    public WebAppWrapper(IApplicationBuilder builder, IServiceProvider services)
    {
        _builder = builder;
        _services = services;

        // Initialize routing middleware — this populates __EndpointRouteBuilder
        // in Properties (a fresh Properties dictionary since the builder is standalone).
        _ = _builder.UseRouting();

        if (!_builder.Properties.TryGetValue("__EndpointRouteBuilder", out var endpointRouteBuilder)
            || endpointRouteBuilder is not IEndpointRouteBuilder erb)
        {
            throw new InvalidOperationException(
                "Could not obtain an IEndpointRouteBuilder from the application builder. " +
                "Ensure that routing services are registered and that the provided IApplicationBuilder supports endpoint routing (UseRouting).");
        }
        _endpoints = erb;
    }

    /// <summary>Seals the route table. Must be called after all MapXxx calls.</summary>
    public void FinalizeEndpoints() => _builder.UseEndpoints(_ => { });

    // ── IApplicationBuilder ──────────────────────────────────────────────────

    // The setter intentionally updates only _services (not _builder.ApplicationServices).
    // The wrapper's purpose is to present the per-test service provider to middleware
    // and endpoint routing while the underlying builder retains its original provider.
    IServiceProvider IApplicationBuilder.ApplicationServices
    {
        get => _services;
        set => _services = value;
    }

    IFeatureCollection IApplicationBuilder.ServerFeatures => _builder.ServerFeatures;

    IDictionary<string, object?> IApplicationBuilder.Properties => _builder.Properties;

    IApplicationBuilder IApplicationBuilder.Use(Func<RequestDelegate, RequestDelegate> middleware)
    {
        _ = _builder.Use(middleware);
        return this;
    }

    IApplicationBuilder IApplicationBuilder.New() => _builder.New();

    RequestDelegate IApplicationBuilder.Build() => _builder.Build();

    // ── IEndpointRouteBuilder ────────────────────────────────────────────────

    IServiceProvider IEndpointRouteBuilder.ServiceProvider => _services;

    ICollection<EndpointDataSource> IEndpointRouteBuilder.DataSources => _endpoints.DataSources;

    IApplicationBuilder IEndpointRouteBuilder.CreateApplicationBuilder() =>
        _endpoints.CreateApplicationBuilder();
}
