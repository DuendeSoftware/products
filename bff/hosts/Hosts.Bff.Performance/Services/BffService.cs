// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Builder;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Hosts.Bff.Performance.Services;

public abstract class BffService(string[] urlConfigKeys, IConfiguration config, IOptions<BffSettings> bffSettings) : BackgroundService
{
    public IConfiguration Config { get; } = config;
    public BffSettings Settings { get; } = bffSettings.Value;

    protected override async Task ExecuteAsync(Ct stoppingToken)
    {
        var urls = urlConfigKeys
            .Select(x => Config[x])
            .OfType<string>()
            .ToArray();

        var builder = WebApplication.CreateBuilder();
        _ = builder.AddServiceDefaults();
        // Configure Kestrel to listen on the specified Uri
        _ = builder.WebHost.UseUrls(urls);

        _ = builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
        });

        _ = builder.Services.AddAuthorization();
        ConfigureServices(builder.Services);

        var bffBuilder = builder.Services.AddBff()
            .AddRemoteApis();

        ConfigureBff(bffBuilder);

        // Build and run the web app
        var app = builder.Build();
        _ = app.UseForwardedHeaders();

        _ = app.UseHttpsRedirection();

        _ = app.UseRouting();

        _ = app.UseAuthentication();
        _ = app.UseAuthorization();

        _ = app.UseBff();

        ConfigureApp(app);

        _ = app.MapGet("/local_anon", () => DateTime.Now.ToString("s"))
            .AsBffApiEndpoint()
            .AllowAnonymous();

        _ = app.MapGet("/local", () => DateTime.Now.ToString("s"))
            .RequireAuthorization()
            .AsBffApiEndpoint();

        _ = app.MapRemoteBffApiEndpoint("/remote_anon", Settings.ApiUrl)
            .WithAccessToken(RequiredTokenType.None);


        _ = app.MapRemoteBffApiEndpoint("/remote_user", Settings.ApiUrl)
            .WithAccessToken();

        _ = app.MapRemoteBffApiEndpoint("/remote_client", Settings.ApiUrl)
            .WithAccessToken(RequiredTokenType.Client);

        // Todo: Make sure this is mapped implicitly
        app.MapBffManagementEndpoints();

        await app.RunAsync(stoppingToken);
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public virtual void ConfigureBff(IBffServicesBuilder bff)
    {
    }

    public virtual void ConfigureApp(WebApplication app)
    {
    }
}
