// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Blazor;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.Blazor.Components;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.TestHost;
using Xunit.Abstractions;
using Yarp.ReverseProxy.Forwarder;

namespace Duende.Hosts.Tests;
public class BffApplicationBuilderTests
{
    private readonly ITestOutputHelper _output;
    private readonly Uri _baseAddress = new Uri("https://bff-builder");
    private readonly BffApplicationBuilder _bffBuilder;

    public BffApplicationBuilderTests(ITestOutputHelper output)
    {
        Context = new TestHostContext(output);
        _output = output;
        _bffBuilder = BffApplicationBuilder.Create(new WebApplicationOptions()
        {
            EnvironmentName = Environments.Development
        });

        _bffBuilder.WebHost.UseTestServer();
        _bffBuilder.Services.AddSingleton<IForwarderHttpClientFactory>(
            new CallbackForwarderHttpClientFactory(
                context => new HttpMessageInvoker(Context.Internet)));

        _bffBuilder.Services.AddLogging(l => l.AddProvider(new TestLoggerProvider(_output.WriteLine, "https://bff-builder")));

        IdentityServer = new IdentityServerTestHost(Context);
        Api = new ApiHost(Context, IdentityServer);
        The.Authority = IdentityServer.Url();

    }

    public TestData The => Context.The;
    public TestDataBuilder Some => new TestDataBuilder(The);
    public TestHostContext Context { get; set; }

    public ApiHost Api { get; set; }
    public IdentityServerTestHost IdentityServer { get; }

    [Fact]
    public async Task Can_proxy_remote_api()
    {
        _bffBuilder.AddRemoteApis();

        var app = _bffBuilder.Build();

        app.MapGet("/", () => "ok");
        app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
            .WithAccessToken(RequiredTokenType.None);

        var client = await InitializeAsync(app);

        await client.CallBffHostApi(The.PathAndSubPath);

    }

    [Fact]
    public async Task Can_login()
    {
        _bffBuilder.AddRemoteApis();

        _bffBuilder.WithDefaultOpenIdConnectOptions(opt =>
        {
            The.DefaultOpenIdConnectConfiguration(opt);
            opt.BackchannelHttpHandler = Context.Internet;
        });

        var app = _bffBuilder.Build();

        app.MapGet("/", () => "ok");
        app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
            .WithAccessToken(RequiredTokenType.User);

        var client = await InitializeAsync(app);

        await client.Login();
        await client.CallBffHostApi(The.PathAndSubPath);
    }

    [Fact]
    public async Task Can_load_config_from_default_config_source()
    {
        _bffBuilder.Configuration
            .AddJson(new BffConfiguration()
            {
                Frontends = new Dictionary<string, BffFrontendConfiguration>()
                {
                    [The.FrontendName] = Some.BffFrontendConfiguration() with
                    {
                        IndexHtmlUrl = null,
                        MatchingPath = The.Path,
                        MatchingOrigin = null
                    },
                }
            })
            .Build();

        var app = _bffBuilder.Build();

        app.Frontends.GetAll().Count().ShouldBe(1);

        app.MapGet("/", (SelectedFrontend s) => s.Get().Name.ToString());

        var client = await InitializeAsync(app);
        await client.GetAsync(The.Path)
            .CheckResponseContent(The.FrontendName);
    }

    [Fact]
    public async Task Can_load_config_from_a_custom_source()
    {
        var config = new ConfigurationBuilder()
            .AddJson(new
            {
                Bff = new BffConfiguration()
                {
                    Frontends = new Dictionary<string, BffFrontendConfiguration>()
                    {
                        [The.FrontendName] = Some.BffFrontendConfiguration() with
                        {
                            IndexHtmlUrl = null,
                            MatchingPath = The.Path,
                            MatchingOrigin = null
                        },
                    }
                }
            })
            .Build();

        _bffBuilder.LoadConfiguration(config.GetSection("Bff"));
        var app = _bffBuilder.Build();

        app.Frontends.GetAll().Count().ShouldBe(1);

        app.MapGet("/", (SelectedFrontend s) => s.Get().Name.ToString());


        var client = await InitializeAsync(app);
        await client.GetAsync(The.Path)
            .CheckResponseContent(The.FrontendName);
    }

    [Fact]
    public async Task Can_load_blazor_home()
    {
        _bffBuilder.WithDefaultOpenIdConnectOptions(opt =>
        {
            The.DefaultOpenIdConnectConfiguration(opt);
            opt.BackchannelHttpHandler = Context.Internet;
        });

        _bffBuilder.Services.AddCascadingAuthenticationState();
        _bffBuilder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        _bffBuilder.Services.AddAntiforgery();

        _bffBuilder.AddBlazorServer()
            .AddServerSideSessions();

        var app = _bffBuilder.Build();

        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode();

        });

        var client = await InitializeAsync(app);
        await client.GetAsync("/")
            .CheckHttpStatusCode();

        await client.Login();
        await client.GetAsync("/secure")
            .CheckHttpStatusCode();
    }

    private async Task<BffHttpClient> InitializeAsync(BffApplication app)
    {
        await app.StartAsync();
        var testServer = app.GetTestServer();

        await Api.InitializeAsync();
        await IdentityServer.InitializeAsync();

        Context.Internet.AddHandler(IdentityServer);
        Context.Internet.AddHandler(Api);

        IdentityServer.AddClient(The.ClientId, _baseAddress);
        Context.Internet.AddHandler(_baseAddress, testServer.CreateHandler());

        var cookieContainer = new CookieContainer();
        var cookieHandler = new CookieHandler(Context.Internet, cookieContainer);
        var redirectHandler = new RedirectHandler(Context.WriteOutput)
        {
            InnerHandler = cookieHandler
        };
        return new BffHttpClient(redirectHandler, cookieContainer, IdentityServer)
        {
            BaseAddress = _baseAddress
        };
    }
}
