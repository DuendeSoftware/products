// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AspNetCore.TestFramework;

public class GenericHost
{
    protected readonly string _baseAddress;

    private readonly ITestOutputHelper _testOutputHelper;
    private IServiceProvider _appServices = default!;

    private TestBrowserClient? _browserClient;

    private HttpClient? _httpClient;
    private AuthenticationProperties? _propsToSignIn;
    private TestServer? _server;

    private ClaimsPrincipal? _userToSignIn;

    public GenericHost(ITestOutputHelper testOutputHelper, string baseAddress = "https://server")
    {
        if (baseAddress.EndsWith("/"))
        {
            baseAddress = baseAddress.Substring(0, baseAddress.Length - 1);
        }

        _baseAddress = baseAddress;
        _testOutputHelper = testOutputHelper;
    }

    public Assembly? HostAssembly { get; set; }
    public bool IsDevelopment { get; set; }

    public TestServer Server
    {
        get => _server ?? throw new InvalidOperationException(
            $"Attempt to use {nameof(Server)} before it was initialized. Did you forget to call {nameof(Initialize)}");
        private set => _server = value;
    }

    public TestBrowserClient BrowserClient
    {
        get =>
            _browserClient ?? throw new InvalidOperationException(
                $"Attempt to use {nameof(BrowserClient)} before is was initialized. Did you forget to call {nameof(Initialize)}");
        private set => _browserClient = value;
    }

    public HttpClient HttpClient
    {
        get =>
            _httpClient ?? throw new InvalidOperationException(
                $"Attempt to use ${nameof(HttpClient)} before is was initialized. Did you forget to call {nameof(Initialize)}");
        private set => _httpClient = value;
    }

    public string Url(string? path = null)
    {
        path ??= string.Empty;
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        return _baseAddress + path;
    }

    public async Task Initialize()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            EnvironmentName = IsDevelopment ? "Development" : "Production",
            ApplicationName = HostAssembly?.GetName()?.Name
        });
        builder.WebHost
            .UseTestServer();

        ConfigureServices(builder.Services);
        var webApplication = builder.Build();
        Configure(webApplication);

        await webApplication.StartAsync();

        Server = webApplication.GetTestServer();
        BrowserClient = new TestBrowserClient(Server.CreateHandler());
        HttpClient = Server.CreateClient();
    }

    public event Action<IServiceCollection> OnConfigureServices = services => { };
    public event Action<WebApplication> OnConfigure = app => { };

    private void ConfigureServices(IServiceCollection services)
    {
        // This adds log messages to the output of our tests when they fail.
        // See https://github.com/martincostello/xunit-logging
        services.AddLogging(options =>
        {
            // If you need different log output to understand a test failure, configure it here
            options.SetMinimumLevel(LogLevel.Error);
            options.AddFilter("Duende", LogLevel.Information);
            options.AddFilter("Duende.IdentityServer.License", LogLevel.Error);
            options.AddFilter("Duende.IdentityServer.Startup", LogLevel.Error);

            options.AddXUnit(_testOutputHelper);
        });

        OnConfigureServices(services);
        _appServices = services.BuildServiceProvider();
    }

    private void Configure(WebApplication builder)
    {
        OnConfigure(builder);

        ConfigureSignin(builder);
        ConfigureSignout(builder);
    }

    private void ConfigureSignout(WebApplication app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/__signout")
            {
                await ctx.SignOutAsync();
                ctx.Response.StatusCode = 204;
                return;
            }

            await next();
        });

    private void ConfigureSignin(WebApplication app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/__signin")
            {
                if (_userToSignIn is not object)
                {
                    throw new Exception("No User Configured for SignIn");
                }

                var props = _propsToSignIn ?? new AuthenticationProperties();
                await ctx.SignInAsync(_userToSignIn, props);

                _userToSignIn = null;
                _propsToSignIn = null;

                ctx.Response.StatusCode = 204;
                return;
            }

            await next();
        });

    public async Task IssueSessionCookieAsync(params Claim[] claims)
    {
        _userToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"));
        var response = await BrowserClient.GetAsync(Url("__signin"));
        response.StatusCode.ShouldBe((HttpStatusCode)204);
    }

    public Task IssueSessionCookieAsync(AuthenticationProperties props, params Claim[] claims)
    {
        _propsToSignIn = props;
        return IssueSessionCookieAsync(claims);
    }
}
