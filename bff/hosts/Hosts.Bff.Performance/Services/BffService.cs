// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Yarp;

namespace Hosts.Bff.Performance.Services;

public abstract class BffService(BffServiceSettings settings) : BackgroundService
{
    public BffServiceSettings Settings { get; } = settings;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();

        // Configure Kestrel to listen on the specified Uri
        builder.WebHost.UseUrls(Settings.Uri);

        builder.Services.AddAuthorization();
        ConfigureServices(builder.Services);

        var bffBuilder = builder.Services.AddBff()
            .AddRemoteApis();

        ConfigureBff(bffBuilder);

        // Build and run the web app
        var app = builder.Build();

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseBff();

        ConfigureApp(app);

        app.MapGet("/local_anon", () => DateTime.Now.ToString("s"))
            .AsBffApiEndpoint()
            .AllowAnonymous();

        app.MapGet("/local", () => DateTime.Now.ToString("s"))
            .RequireAuthorization()
            .AsBffApiEndpoint();

        app.MapRemoteBffApiEndpoint("/remote_anon", Settings.ApiUrl)
            .WithAccessToken(RequiredTokenType.None);


        app.MapRemoteBffApiEndpoint("/remote_user", Settings.ApiUrl)
            .WithAccessToken();

        app.MapRemoteBffApiEndpoint("/remote_client", Settings.ApiUrl)
            .WithAccessToken(RequiredTokenType.Client);

        // Todo: Make sure this is mapped implicitly
        app.MapBffManagementEndpoints();

        await app.RunAsync(stoppingToken);
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public virtual void ConfigureBff(BffBuilder bff)
    {
    }

    public virtual void ConfigureApp(WebApplication app)
    {
    }
}
