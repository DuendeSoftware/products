// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Tests.TestFramework;

public class GenericHost(WriteTestOutput writeOutput, string baseAddress = "https://server") : IAsyncDisposable
{
    private readonly string _baseAddress = baseAddress.EndsWith('/')
        ? baseAddress.Substring(0, baseAddress.Length - 1)
        : baseAddress;

    private IServiceProvider _appServices = null!;

    public bool UseForwardedHeaders { get; set; }

    public TestServer Server { get; private set; } = null!;
    public TestBrowserClient BrowserClient { get; private set; } = null!;
    public HttpClient HttpClient { get; private set; } = null!;

    private TestLoggerProvider Logger { get; } = new(writeOutput, baseAddress + " - ");


    public T Resolve<T>() where T : notnull
    {
        // not calling dispose on scope on purpose
        return _appServices.GetRequiredService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<T>();
    }

    public string Url(string? path = null)
    {
        path ??= string.Empty;
        if (!path.StartsWith('/')) path = '/' + path;
        return _baseAddress + path;
    }

    public async Task InitializeAsync()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();

                builder.ConfigureServices(ConfigureServices);
                builder.Configure(app =>
                {
                    if (UseForwardedHeaders)
                    {
                        app.UseForwardedHeaders(new ForwardedHeadersOptions
                        {
                            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                               ForwardedHeaders.XForwardedProto |
                                               ForwardedHeaders.XForwardedHost
                        });
                    }

                    ConfigureApp(app);
                });
            });

        // Build and start the IHost
        var host = await hostBuilder.StartAsync();

        Server = host.GetTestServer();
        BrowserClient = new TestBrowserClient(Server.CreateHandler());
        BrowserClient.BaseAddress = new Uri(_baseAddress);
        HttpClient = Server.CreateClient();
        BrowserClient.BaseAddress = new Uri(_baseAddress);
    }

    public event Action<IServiceCollection> OnConfigureServices = _ => { };
    public event Action<IApplicationBuilder> OnConfigure = _ => { };

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(options =>
        {
            options.SetMinimumLevel(LogLevel.Debug);
            options.AddProvider(Logger);
        });

        OnConfigureServices(services);
    }

    private void ConfigureApp(IApplicationBuilder app)
    {
        _appServices = app.ApplicationServices;

        OnConfigure(app);

        ConfigureSignin(app);
        ConfigureSignout(app);
    }

    private void ConfigureSignout(IApplicationBuilder app)
    {
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
    }

    public async Task RevokeSessionCookieAsync()
    {
        var response = await BrowserClient.GetAsync(Url("__signout"));
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private void ConfigureSignin(IApplicationBuilder app)
    {
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/__signin")
            {
                if (_userToSignIn is null)
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
    }

    private ClaimsPrincipal? _userToSignIn;
    private AuthenticationProperties? _propsToSignIn;
    public async virtual Task IssueSessionCookieAsync(params Claim[] claims)
    {
        _userToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"));
        var response = await BrowserClient.GetAsync(Url("__signin"));
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    protected Task IssueSessionCookieAsync(AuthenticationProperties props, params Claim[] claims)
    {
        _propsToSignIn = props;
        return IssueSessionCookieAsync(claims);
    }
    public Task IssueSessionCookieAsync(string sub, params Claim[] claims)
    {
        return IssueSessionCookieAsync(claims.Append(new Claim("sub", sub)).ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(Server);
        await CastAndDispose(BrowserClient);
        await CastAndDispose(HttpClient);
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
