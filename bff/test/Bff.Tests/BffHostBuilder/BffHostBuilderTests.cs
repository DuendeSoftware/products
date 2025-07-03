// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.Benchmarks;
using Duende.Bff.Configuration;
using Duende.Bff.EntityFramework;
using Duende.Bff.Tests.TestInfra;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.BffHostBuilder;

public class BffHostBuilderTests(ITestOutputHelper output) : BffHostBuilderTestBase(output)
{
    private string DbName = Guid.NewGuid().ToString();
    [Fact]
    public async Task Can_run_bff_with_test_server()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        // Adds log provider to the host
        //hostBuilder.AddServiceDefaults();

        hostBuilder.Logging.ClearProviders();
        hostBuilder.Logging.AddProvider(new TestLoggerProvider(Output.WriteLine, "https://bff-server - "));

        var bffApplication = hostBuilder.AddBffApplication()
            .UsingTestServer();

        //bffApplication.WithServiceDefaults((host, hostType) => host.AddServiceDefaults());

        var bffEndpointBuilder = bffApplication.EnableBffEndpoint();
        bffEndpointBuilder
            .ConfigureOpenIdConnect(options =>
            {
                The.DefaultOpenIdConnectConfiguration(options);
            })
            .ConfigureOptions(options =>
            {
                options.BackchannelHttpHandler = Context.Internet;
            })
            .ConfigureApp(app =>
            {
                app.MapGet("/", () => "ok");
            });

        bffApplication.EnableServerSideSessions()
            .UsingEntityFramework(opt => opt.UseInMemoryDatabase(DbName));

        bffApplication.EnableManagementApi();
        bffApplication.EnableManagementUI();

        using var host = hostBuilder.Build();

        var client = await InitializeAsync(host);

        await client.GetAsync("/").EnsureStatusCode();
        await client.GetAsync("/bff/login").EnsureStatusCode();

        await host.StopAsync();

    }
    [Fact]
    public void Cannot_call_add_bff_twice()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var bff = hostBuilder.AddBffApplication();

        Should.Throw<InvalidOperationException>(() => hostBuilder.AddBffApplication());
    }
    [Fact]
    public void Cannot_call_enable_endpoint_twice()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var bff = hostBuilder.AddBffApplication();

        bff.EnableBffEndpoint();
        Should.Throw<InvalidOperationException>(() => bff.EnableBffEndpoint());
    }

    [Fact]
    public async Task Can_read_config_from_host()
    {
        using var configFile = new ConfigFile();
        configFile.Save(section: "Bff", config: new BffConfiguration()
        {
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                [The.FrontendName] = Some.BffFrontendConfiguration() with
                {
                    MatchingPath = The.Path,
                    MatchingOrigin = null,
                    IndexHtmlUrl = Cdn.Url("index.html")
                }
            }
        });

        var hostBuilder = Host.CreateApplicationBuilder();

        hostBuilder.Logging.ClearProviders();
        hostBuilder.Logging.AddProvider(new TestLoggerProvider(Output.WriteLine, "https://bff-server - "));

        hostBuilder.Configuration.AddJsonFile(configFile.ToString());

        var bffApplication = hostBuilder.AddBffApplication()
            .UsingTestServer();

        bffApplication.EnableBffEndpoint()
            .ConfigureOptions(options =>
            {
                options.BackchannelHttpHandler = Context.Internet;
            });


        using var host = hostBuilder.Build();

        var client = await InitializeAsync(host);

        // Verify that the frontend is loaded by checking the path mapped to the frontend
        await client.GetAsync(The.Path).EnsureStatusCode();

        await host.StopAsync();
    }


    [Fact]
    public async Task Can_run_bff_with_random_port()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        hostBuilder.Logging.AddProvider(new TestLoggerProvider(Output.WriteLine, "bff"));

        var bffApplication = hostBuilder.AddBffApplication();

        bffApplication.EnableBffEndpoint()
            .UseUrls("https://127.0.0.1:0")
            .ConfigureOpenIdConnect(The.DefaultOpenIdConnectConfiguration)
            .ConfigureApp(app =>
            {
                app.MapGet("/", () => "ok");
            })
            .ConfigureOptions(options =>
            {
                options.BackchannelHttpHandler = Context.Internet;
            });

        using var host = hostBuilder.Build();

        await host.StartAsync();


        var client = new HttpClient()
        {
            BaseAddress = host.GetBffUri()
        };
        await client.GetAsync("/").EnsureStatusCode();

        await host.StopAsync();

    }
}
