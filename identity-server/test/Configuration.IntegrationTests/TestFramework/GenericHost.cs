// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.TestFramework;

public class GenericHost
{
    public GenericHost(string baseAddress = "https://server")
    {
        if (baseAddress.EndsWith('/')) baseAddress = baseAddress.Substring(0, baseAddress.Length - 1);
        _baseAddress = baseAddress;
    }

    private readonly string _baseAddress;
    protected IServiceProvider? _appServices;

    public Assembly? HostAssembly { get; set; }
    public bool IsDevelopment { get; set; }

    public TestServer? Server { get; private set; }
    public HttpClient? HttpClient { get; set; }

    public TestLoggerProvider Logger { get; set; } = new TestLoggerProvider();


    public T Resolve<T>()
        where T : notnull
    {
        if (_appServices == null)
        {
            throw new Exception("Attempt to resolve services before service provider created. Call ConfigureApp first");
        }
        // not calling dispose on scope on purpose
        return _appServices.GetRequiredService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<T>();
    }

    public string Url(string path = "")
    {
        if (!path.StartsWith('/')) path = '/' + path;
        return _baseAddress + path;
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = IsDevelopment ? "Development" : "Production"
        });
        builder.WebHost.UseTestServer();

        if (HostAssembly is not null)
        {
            builder.Environment.ApplicationName = HostAssembly.GetName().Name ?? "";
        }

        ConfigureServices(builder.Services);
        var app = builder.Build();
        ConfigureApp(app);

        // Build and start the IHost
        await app.StartAsync();

        Server = app.GetTestServer();
        // BrowserClient = new TestBrowserClient(Server.CreateHandler());
        HttpClient = Server.CreateClient();
    }

    public event Action<IServiceCollection> OnConfigureServices = services => { };
    public event Action<WebApplication> OnConfigure = app => { };

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(options =>
        {
            options.SetMinimumLevel(LogLevel.Critical);
            options.AddProvider(Logger);
        });

        OnConfigureServices(services);
    }

    private void ConfigureApp(WebApplication app)
    {
        _appServices = app.Services;

        OnConfigure(app);
    }
}
