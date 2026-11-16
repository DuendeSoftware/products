// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.TestFramework;

internal sealed class KestrelTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private KestrelTestHost(WebApplication app) => _app = app;

    public string Uri()
    {
        var server = _app.Services.GetRequiredService<IServer>();
        return server.Features.Get<IServerAddressesFeature>()!.Addresses.First().Replace("127.0.0.1", "localhost");
    }

    public static async Task<KestrelTestHost> Create(
        Action<IServiceCollection> configureServices,
        Action<WebApplication> configureApp,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("https://127.0.0.1:0");
        configureServices(builder.Services);
        var app = builder.Build();
        configureApp(app);
        await app.StartAsync(cancellationToken);
        return new KestrelTestHost(app);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
