// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityServer.PerfTest.Infrastructure;

public class IdentityServerContainer : IDisposable
{
    public const string BaseAddress = "https://server";

    public string Url(string path = null)
    {
        if (!path.StartsWith('/')) path = '/' + path;
        return BaseAddress + path;
    }

    public TestServer Server { get; private set; }
    public HttpMessageHandler Handler { get; private set; }
    //public BrowserClient BrowserClient { get; set; }
    public HttpClient BackChannelClient { get; set; }

    public DebugLoggerProvider Logger { get; set; } = new DebugLoggerProvider();

    private IServiceProvider _appServices;
    public T ResolveService<T>()
    {
        return _appServices.GetRequiredService<T>();
    }

    private List<IServiceScope> _serviceScopes = new List<IServiceScope>();

    public IServiceScope CreateServiceScope()
    {
        return ResolveService<IServiceScopeFactory>().CreateScope();
    }
    public T ResolveScopedService<T>()
    {
        // caching to call dispose later
        var scope = CreateServiceScope();
        _serviceScopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    public void Dispose()
    {
        DisposeServices();
        Handler.Dispose();
        Server.Dispose();
    }

    public void DisposeServices()
    {
        foreach (var scope in _serviceScopes)
        {
            scope.Dispose();
        }
        _serviceScopes.Clear();
    }


    public void Reset()
    {
        ResetAsync().GetAwaiter().GetResult();
    }

    public async Task ResetAsync()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();
                builder.UseSetting("Environment", "Production");

                builder.ConfigureServices(ConfigureServices);
                builder.Configure(ConfigureApp);
            });

        // Build and start the IHost
        var host = await hostBuilder.StartAsync();

        Server = host.GetTestServer();
        Handler = Server.CreateHandler();

        //BrowserClient = new BrowserClient(new BrowserHandler(Handler));
        BackChannelClient = new HttpClient(Handler);

        //MockOidcHandler = Resolve<OpenIdConnectHandler>() as MockOidcHandler;
    }

    public event Action<IServiceCollection> OnConfigureServices = services => { };
    public event Action<IServiceCollection> OnPostConfigureServices = services => { };
    public event Action<IApplicationBuilder> OnConfigure = app => { };

    //public MockMessageHandler BackChannelMessageHandler { get; set; } = new MockMessageHandler();
    //public MockMessageHandler JwtRequestMessageHandler { get; set; } = new MockMessageHandler();

    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Information;
    public Dictionary<string, LogLevel> LogFilters { get; set; } = new Dictionary<string, LogLevel>
    {
    };

    //options.AddFilter("Duende.IdentityServer", LogLevel.Debug);
    //options.AddFilter("Solliance", LogLevel.Debug);


    public void ConfigureServices(IServiceCollection services)
    {
        var logging = services.AddLogging(options =>
        {
            options.SetMinimumLevel(DefaultLogLevel);
            options.AddProvider(Logger);

            foreach (var item in LogFilters)
            {
                options.AddFilter(item.Key, item.Value);
            }
        });

        OnConfigureServices(services);

        // this replaces the OIDC handler so we can fake the outbound/inbound protocol request/response
        //services.AddSingleton<OpenIdConnectHandler, MockOidcHandler>();

        OnPostConfigureServices(services);
    }

    public void ConfigureApp(IApplicationBuilder app)
    {
        _appServices = app.ApplicationServices;

        OnConfigure(app);
    }

    public HttpClient CreateClient()
    {
        return new HttpClient(Handler);
    }
}
