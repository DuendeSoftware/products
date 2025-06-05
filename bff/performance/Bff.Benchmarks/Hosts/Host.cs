// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bff.Benchmarks.Hosts;

public abstract class Host : IAsyncDisposable
{
    private WebApplication _app = null!;
    private WebApplicationBuilder _builder = null!;

    public event Action<IServiceCollection> OnConfigureServices = _ => { };
    public event Action<WebApplication> OnConfigure = _ => { };

    public Host()
    {
        _builder = WebApplication.CreateBuilder();
        // Logs interfere with the benchmarks, so we clear them
        _builder.Logging.ClearProviders();

        // Ensure dev certificate is used for SSL
        _builder.WebHost
            .UseUrls("https://127.0.0.1:0");

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
    }

    public Uri Url => new Uri("https://localhost:" + new Uri(_app.Urls.First()).Port);
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
