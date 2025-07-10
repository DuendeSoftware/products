// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bff.Benchmarks.Hosts;

public abstract class Host : IAsyncDisposable
{
    private Uri _uri;
    internal SimulatedInternet Internet { get; }

    public Uri Url(string? path = null) => path == null ? _uri : new Uri(_uri, path);

    private WebApplication _app = null!;
    private WebApplicationBuilder _builder = null!;
    public TestServer Server { get; private set; } = null!;

    public event Action<IServiceCollection> OnConfigureServices = _ => { };
    public event Action<WebApplication> OnConfigure = _ => { };

    internal Host(Uri uri, SimulatedInternet simulatedInternet)
    {
        _uri = uri;
        Internet = simulatedInternet;
        _builder = WebApplication.CreateBuilder();
        // Logs interfere with the benchmarks, so we clear them




        // Ensure dev certificate is used for SSL
        if (Internet.UseKestrel)
        {
            _builder.Logging.ClearProviders();
            //_builder.Logging.AddSerilog(Internet.Log);

            _builder.WebHost.UseUrls("https://*:0");
        }
        else
        {
            _builder.Logging.AddConsole();
            _builder.WebHost
                .UseTestServer();
        }



        _builder.Services.AddAuthentication();
        _builder.Services.AddAuthorization();
        _builder.Services.AddRouting();
    }

    public T GetService<T>() where T : notnull => _app.Services.GetRequiredService<T>();

    public void Initialize()
    {
        OnConfigureServices(_builder.Services);

        _app = _builder.Build();

        OnConfigure(_app);

        _app.Start();

        if (Internet.UseKestrel)
        {
            _uri = new Uri("https://localhost:" + new Uri(_app.Urls.First()).Port);
        }
        else
        {

            Server = _app.GetTestServer();

            Internet.AddHandler(this);
        }

    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
