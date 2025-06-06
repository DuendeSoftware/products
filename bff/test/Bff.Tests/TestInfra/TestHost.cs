// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.TestHost;

namespace Duende.Bff.Tests.TestInfra;

public class TestHost(TestHostContext context, Uri baseAddress) : IAsyncDisposable
{
    public TestHost(TestHostContext context) : this(context, new("https://server"))
    {
    }

    public TestDataBuilder Some => context.Some;
    public TestData The => context.The;

    protected SimulatedInternet Internet => context.Internet;

    IServiceProvider? _appServices = null!;

    public TestServer Server { get; private set; } = null!;

    private TestLoggerProvider Logger { get; } = new(context.WriteOutput, baseAddress + " - ");

    public T Resolve<T>() where T : notnull
    {
        if (_appServices == null)
        {
            throw new InvalidOperationException("Not yet initialized");
        }
        // not calling dispose on scope on purpose
        return _appServices.GetRequiredService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<T>();
    }

    public Uri Url(string? path = null)
    {
        path ??= string.Empty;
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        return new Uri(baseAddress, path);
    }

    public virtual void Initialize()
    {

    }

    public async Task InitializeAsync()
    {
        Initialize();

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();

                builder.ConfigureServices(ConfigureServices);
                builder.Configure(ConfigureApp);
            });

        // Build and start the IHost
        var host = await hostBuilder.StartAsync();
        Server = host.GetTestServer();

        context.Internet.AddHandler(this);
    }


    public event Action<IServiceCollection> OnConfigureServices = _ => { };
    public event Action<IApplicationBuilder> OnConfigure = _ => { };
    public event Action<IEndpointRouteBuilder> OnConfigureEndpoints = _ => { };

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(The.Clock);

        services.AddAuthentication();
        services.AddAuthorization();
        services.AddRouting();

        services.AddLogging(options =>
        {
            options.SetMinimumLevel(LogLevel.Debug);
            options.AddProvider(Logger);
        });

        OnConfigureServices(services);
    }

    protected virtual void ConfigureApp(IApplicationBuilder app)
    {
        _appServices = app.ApplicationServices;
        app.Use(async (c, n) =>
        {
            await n();
        });
        OnConfigure(app);

        app.UseEndpoints(endpoints =>
        {
            OnConfigureEndpoints(endpoints);
        });
    }

    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(Server);
        await CastAndDispose(Logger);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
            {
                await resourceAsyncDisposable.DisposeAsync();
            }
            else
            {
                resource.Dispose();
            }
        }
    }
}
