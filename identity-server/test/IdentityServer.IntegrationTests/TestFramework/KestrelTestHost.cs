// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable CS8602

using System.Net;
using Duende.IdentityServer.IntegrationTests.Endpoints.Saml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.TestFramework;

internal class KestrelTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly int _portNumber;

    private KestrelTestHost(WebApplication app, int portNumber)
    {
        _app = app;
        _portNumber = portNumber;
    }

    public string Uri() => $"https://localhost:{_portNumber}";

    public static async Task<KestrelTestHost> Create(
        ITestOutputHelper output,
        Action<IServiceCollection> configureServices,
        Action<WebApplication> configureApp,
        CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("https://127.0.0.1:0");
        builder.Logging.AddXUnit(output);
        configureServices(builder.Services);
        var app = builder.Build();
        configureApp(app);
        await app.StartAsync(ct);

        var uri = app.GetBaseUri();
        return new KestrelTestHost(app, uri.Port);
    }

    public HttpClient CreateClient(bool allowAutoRedirect = true) => new(
        new CookieHandler(new HttpClientHandler { AllowAutoRedirect = allowAutoRedirect }, new CookieContainer()))
    {
        BaseAddress = new Uri($"https://localhost:{_portNumber}")
    };

    public IServiceProvider ConfiguredServices => _app.Services;

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

public static class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        public Uri GetBaseUri()
        {
            var server = app.Services.GetRequiredService<IServer>();
            var serverAddress = server.Features.Get<IServerAddressesFeature>();
            var url = serverAddress.Addresses.First();
            return new Uri(url);
        }

        public HttpClient CreateClient(bool allowAutoRedirect = true) => new(
            new CookieHandler(new HttpClientHandler { AllowAutoRedirect = allowAutoRedirect }, new CookieContainer()))
        {
            BaseAddress = app.GetBaseUri()
        };
    }
}
