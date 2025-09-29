// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using Duende.IdentityServer.EndToEndTests.TestInfra;
using Duende.Xunit.Playwright;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Projects;
using Serilog;
using Xunit.Abstractions;
using IBrowser = Duende.IdentityModel.OidcClient.Browser.IBrowser;

namespace Duende.IdentityServer.IntegrationTests;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class MyTest(ITestOutputHelper output, IdentityServerHostTestFixture fixture) : PlaywrightTestBase<Dev>(output, fixture)
{
    private HostApplicationBuilder _builder;
    OidcClient _oidcClient;
    private IHttpClientFactory _httpClientFactory;
    [Fact]
    public async Task CanDoIt()
    {
        _builder = Host.CreateApplicationBuilder();

        // Add ServiceDefaults from Aspire
        _builder.AddServiceDefaults();

        // Register a named HttpClient with service discovery support.
        // The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
        _builder.Services.AddHttpClient("SimpleApi", client =>
        {
            client.BaseAddress = Fixture.GetUrlTo("dpop-api");
        });

        using var host = _builder.Build();
        _httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

        await host.StartAsync();
        var result = await SignIn();
        await CallApi(result.AccessToken);
        await host.StopAsync();

    }
    async Task CallApi(string currentAccessToken)
    {
        // Resolve the HttpClient from DI.
        var _apiClient = _httpClientFactory.CreateClient("SimpleApi");

        _apiClient.SetBearerToken(currentAccessToken);
        var response = await _apiClient.GetAsync("identity");

        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        //if (response.IsSuccessStatusCode)
        //{
        //    var json = await response.Content.ReadAsStringAsync();
        //    Console.WriteLine("API Response:");
        //    Console.WriteLine(json);
        //}
        //else
        //{
        //    Console.WriteLine($"Error: {response.ReasonPhrase}");
        //}
    }

    async Task<LoginResult> SignIn()
    {
        // Resolve the authority from the configuration.
        var authority = _builder.Configuration["is-host"];
        var browser = new PlaywrightBrowser(Page);

        // Create a redirect URI using an available port on the loopback address.
        // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
        var redirectUri = $"http://127.0.0.1:{browser.Port}";

        var options = new OidcClientOptions
        {
            Authority = authority,
            ClientId = "console.pkce",
            RedirectUri = redirectUri,
            Scope = "openid profile resource1.scope1",
            FilterClaims = false,
            Browser = browser
        };

        var serilog = new LoggerConfiguration()
            .MinimumLevel.Error()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
            .CreateLogger();

        options.LoggerFactory.AddSerilog(serilog);

        _oidcClient = new OidcClient(options);
        var result = await _oidcClient.LoginAsync(new LoginRequest());

        result.IsError.ShouldBe(false);
        return result;

    }
}

public class PlaywrightBrowser : IBrowser
{
    public int Port { get; }
    private readonly IPage _page;
    private readonly string _path;

    public PlaywrightBrowser(IPage page, int? port = null, string path = null)
    {
        _page = page;
        _path = path;

        if (!port.HasValue)
        {
            Port = GetRandomUnusedPort();
        }
        else
        {
            Port = port.Value;
        }
    }

    private int GetRandomUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        using (var listener = new LoopbackHttpListener(Port, _path))
        {
            await Login(options.StartUrl);

            try
            {
                var result = await listener.WaitForCallbackAsync();
                if (string.IsNullOrWhiteSpace(result))
                {
                    return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = "Empty response." };
                }

                return new BrowserResult { Response = result, ResultType = BrowserResultType.Success };
            }
            catch (TaskCanceledException ex)
            {
                return new BrowserResult { ResultType = BrowserResultType.Timeout, Error = ex.Message };
            }
            catch (Exception ex)
            {
                return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
            }
        }
    }

    public async Task Login(string url)
    {
        await _page.GotoAsync(url);
        await _page.GetByPlaceholder("Username").ClickAsync();
        await _page.GetByPlaceholder("Username").FillAsync("alice");
        await _page.GetByPlaceholder("Password").ClickAsync();
        await _page.GetByPlaceholder("Password").FillAsync("alice");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
    }
}

public class LoopbackHttpListener : IDisposable
{
    private const int DefaultTimeout = 5; // 5 sec

    private IWebHost _host;
    private TaskCompletionSource<string> _source = new TaskCompletionSource<string>();
    private string _url;

    public string Url => _url;

    public LoopbackHttpListener(int port, string path = null)
    {
        path = path ?? string.Empty;
        if (path.StartsWith('/'))
        {
            path = path.Substring(1);
        }

        _url = $"http://127.0.0.1:{port}/{path}";

        _host = new WebHostBuilder()
            .UseKestrel()
            .UseUrls(_url)
            .Configure(Configure)
            .Build();
        _host.Start();
    }

    public void Dispose() => Task.Run(async () =>
    {
        await Task.Delay(500);
        _host.Dispose();
    });

    private void Configure(IApplicationBuilder app) => app.Run(async ctx =>
    {
        if (ctx.Request.Method == "GET")
        {
            await SetResultAsync(ctx.Request.QueryString.Value, ctx);
        }
        else if (ctx.Request.Method == "POST")
        {
            if (!ctx.Request.ContentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 415;
            }
            else
            {
                using (var sr = new StreamReader(ctx.Request.Body, Encoding.UTF8))
                {
                    var body = await sr.ReadToEndAsync();
                    await SetResultAsync(body, ctx);
                }
            }
        }
        else
        {
            ctx.Response.StatusCode = 405;
        }
    });

    private async Task SetResultAsync(string value, HttpContext ctx)
    {
        try
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync("<h1>You can now return to the application.</h1>");
            await ctx.Response.Body.FlushAsync();

            _source.TrySetResult(value);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync("<h1>Invalid request.</h1>");
            await ctx.Response.Body.FlushAsync();
        }
    }

    public Task<string> WaitForCallbackAsync(int timeoutInSeconds = DefaultTimeout)
    {
        Task.Run(async () =>
        {
            await Task.Delay(timeoutInSeconds * 1000);
            _source.TrySetCanceled();
        });

        return _source.Task;
    }
}

