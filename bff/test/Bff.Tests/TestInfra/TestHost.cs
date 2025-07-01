// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.TestHost;

namespace Duende.Bff.Tests.TestInfra;

public class TestHost(TestHostContext context, Uri baseAddress) : IAsyncDisposable
{
    internal TestDataBuilder Some => context.Some;
    public TestData The => context.The;

    protected SimulatedInternet Internet => context.Internet;

    protected void WriteOutput(string output) => context.WriteOutput(output);

    IServiceProvider? _appServices = null!;

    public TestServer Server { get; private set; } = null!;

    private TestLoggerProvider Logger { get; } = new(context.WriteOutput, baseAddress.Host + " - ");

    /// <summary>
    /// Allows you to resolve a service directly from the container AS IF it's requested for a specific frontend
    /// Normally, this can only happen if the request is made in a HTTP context, but this method allows you to simulate that.
    ///
    /// This is needed because the SelectedFrontend is set in the HttpContext, and some services depend on that.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ScopedServiceProvider ResolveForFrontend(BffFrontend? selectedFrontend)
    {
        if (_appServices == null)
        {
            throw new InvalidOperationException("Not yet initialized");
        }

        // not calling dispose on scope on purpose
        var serviceScope = _appServices.GetRequiredService<IServiceScopeFactory>()
            .CreateScope();

        // Simulate the fact that we're running in a http context. 
        var accessor = serviceScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        if (selectedFrontend != null)
        {
            // Set the frontend in the HttpContext. 
            serviceScope.ServiceProvider.GetRequiredService<CurrentFrontendAccessor>()
                .Set(selectedFrontend);
        }

        // return a ScopedServiceProvider that will reset the HttpContext when disposed
        // This allows callers to control how long this http context is valid
        return new ScopedServiceProvider(serviceScope, () => accessor.HttpContext = null);
    }

    public T Resolve<T>() where T : notnull
    {
        if (_appServices == null)
        {
            throw new InvalidOperationException("Not yet initialized");
        }

        // not calling dispose on scope on purpose
        var serviceScope = _appServices.GetRequiredService<IServiceScopeFactory>()
            .CreateScope();

        return serviceScope
            .ServiceProvider
            .GetRequiredService<T>();
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
        app.Use(async (c, n) => { await n(); });
        OnConfigure(app);

        app.UseEndpoints(endpoints => { OnConfigureEndpoints(endpoints); });
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

    public class ScopedServiceProvider(IServiceScope scope, Action onDispose) : IDisposable
    {
        public T Resolve<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

        public void Dispose()
        {
            onDispose();
            scope.Dispose();
        }
    }
}
