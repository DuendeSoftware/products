// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.UI.DevPortal;

public sealed class DevPortalTestHost : TestHost
{
    private readonly string _renderedHtml;
    private readonly IReadOnlyList<ScenarioLink> _links;

    public DevPortalTestHost(
        IScenarioConfigurator configurator,
        string htmlContent,
        IReadOnlyList<ScenarioLink> links)
        : base(configurator, "devportal")
    {
        _renderedHtml = htmlContent;
        _links = links;
    }

    protected override WebApplication CreateApp(WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton(_renderedHtml);
        services.AddSingleton(_links);

        services.AddRazorPages()
            .WithRazorPagesRoot("/DevPortal/Pages")
            .AddApplicationPart(typeof(DevPortalTestHost).Assembly);

        var app = builder.Build();

        app.UseRouting();
        app.MapRazorPages();

        return app;
    }
}
