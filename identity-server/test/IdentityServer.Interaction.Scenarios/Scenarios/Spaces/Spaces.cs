// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.UI.Infra;
using Duende.Spaces;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Interaction.Scenarios.Spaces;

internal class Spaces : IScenario
{
    private static readonly ActivitySource Source = new("Duende.IdentityServer.Interaction.Scenarios");
    private IdentityServerTestHost? _identityServer;

    public string Name => "Spaces";
    public string Description => "Demonstrates how Spaces works";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // 1. Start IdentityServer
        _identityServer = new IdentityServerTestHost(configurator, "identity-server",
            idsrv =>
            {
                idsrv.AddStorage(opt => opt.AddSqliteInMemoryStore());
                idsrv.Services.AddSpaces();

            }, configureApp: (app, next) =>
            {
                app.UseSpaceResolution();
                next(app);
                app.MapGet("/", async (ISpaceContextAccessor spaceContextAccessor, ILogger<Spaces> logger) =>
                {
                    using (Source.StartActivity("some operation"))
                    {
                        await Task.Delay(100);
                        logger.LogInformation("some message");
                        logger.LogWarning("some warning");
                    }

                    using (Source.StartActivity("some other operation"))
                    {
                        await Task.Delay(100);
                        logger.LogInformation("some message");
                        logger.LogWarning("some warning");
                    }

                    return "Hello World! " + spaceContextAccessor.GetSpaceId();
                });
            })
            ;
        _identityServer.AddDefaultUsers();
        _identityServer.AddDefaultResources();
        
        await _identityServer.StartAsync(ct);

        await _identityServer.App.Services.GetRequiredService<IDatabaseSchema>().MigrateAsync(ct);

        var spaceAdmin = _identityServer.App.Services.GetRequiredService<ISpaceAdmin>();
        await spaceAdmin.CreateAsync(new CreateSpaceConfiguration()
        {
            Name = "space1",
            MatchPatterns = [new SpaceMatchPattern()
            {
                Path = "/space1",
            }]
        }, ct);

        await spaceAdmin.CreateAsync(new CreateSpaceConfiguration()
        {
            Name = "space2",
            MatchPatterns = [new SpaceMatchPattern()
            {
                Path = "/space2",
            }]
        }, ct);


        Links = [
            new ScenarioLink("space1", new Uri(_identityServer.Link.Url, "/t/space1/")),
            new ScenarioLink("space2", new Uri(_identityServer.Link.Url, "/t/space2/"))];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_identityServer != null)
        {
            await _identityServer.DisposeAsync();
        }
    }
    public Command[] GetCommands() => [];
}
