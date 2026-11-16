// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Net;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Http.Features;

namespace Duende.Bff.Tests;

public class StaticAssetsProxyTransportTests : BffTestBase
{
    public StaticAssetsProxyTransportTests()
    {
        Bff.EnableStaticAssetsProxyHandler = false;
        Bff.MapGetForRoot = false;
    }

    [Fact]
    public async Task Kestrel_keeps_an_absolute_looking_request_on_the_assets_host_and_returns_streamed_content()
    {
        var targetRequests = 0;
        var assetRequests = new ConcurrentQueue<string>();
        await using var target = await StartAssetServer(async context =>
        {
            _ = Interlocked.Increment(ref targetRequests);
            await context.Response.WriteAsync("attacker");
        });
        await using var assets = await StartAssetServer(async context =>
        {
            assetRequests.Enqueue(context.Request.Path.Value!);
            await context.Response.StartAsync();
            await context.Response.WriteAsync("asset");
        });

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        _ = builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        _ = builder.Services.AddAuthentication();
        _ = builder.Services.AddAuthorization();
        _ = builder.Services.AddRouting();
        _ = builder.Services.AddBff()
            .AddFrontends(Some.BffFrontend().WithProxiedStaticAssets(AssetBase(assets)));

        await using var app = builder.Build();
        _ = app.UseRouting();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.UseBff();
        await app.StartAsync();

        using var browser = new HttpClient
        {
            BaseAddress = new Uri(app.Urls.Single()),
            Timeout = TimeSpan.FromSeconds(10)
        };
        var targetUrl = target.Urls.Single();
        using var response = await browser.GetAsync("/" + targetUrl + "/secret");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("asset");
        assetRequests.ToArray().ShouldBe(["/assets/" + targetUrl + "/secret"]);
        targetRequests.ShouldBe(0);
    }

    [Fact]
    public async Task Real_http_transport_rejects_an_off_origin_redirect_without_contacting_its_target()
    {
        var targetRequests = 0;
        var assetRequests = new ConcurrentQueue<string>();
        await using var target = await StartAssetServer(context =>
        {
            _ = Interlocked.Increment(ref targetRequests);
            return Task.CompletedTask;
        });
        await using var assets = await StartAssetServer(context =>
        {
            assetRequests.Enqueue(context.Request.Path.Value!);
            context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            context.Response.Headers.Location = target.Urls.Single() + "/secret";
            return Task.CompletedTask;
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(AssetBase(assets)));

        using var response = await Bff.BrowserClient.GetAsync("/start");

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        response.Headers.Location.ShouldBeNull();
        assetRequests.ToArray().ShouldBe(["/assets/start"]);
        targetRequests.ShouldBe(0);
    }

    [Fact]
    public async Task Fallback_rejects_a_same_origin_redirect_without_following_it()
    {
        var requests = new ConcurrentQueue<string>();
        await using var assets = await StartAssetServer(context =>
        {
            var path = context.Request.Path.Value!;
            requests.Enqueue(path);
            if (path == "/assets/missing")
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers.Location = "/assets/next";
            }
            return Task.CompletedTask;
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(AssetBase(assets)));

        using var response = await Bff.BrowserClient.GetAsync("/missing");

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        response.Headers.Location.ShouldBeNull();
        requests.ToArray().ShouldBe(["/assets/missing", "/assets/"]);
    }

    [Theory]
    [InlineData("/\\evil.test")]
    [InlineData("/\t/evil.test")]
    public async Task A_non_redirect_backslash_or_control_character_location_is_not_forwarded(string location)
    {
        await using var assets = await StartAssetServer(context =>
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.Headers.Location = location;
            return Task.CompletedTask;
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(AssetBase(assets)));

        using var response = await Bff.BrowserClient.GetAsync("/asset");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Escaped_asset_filename_is_not_overdecoded()
    {
        string? upstreamTarget = null;
        await using var assets = await StartAssetServer(async context =>
        {
            upstreamTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
            await context.Response.WriteAsync("asset");
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(AssetBase(assets)));

        using var response = await Bff.BrowserClient.GetAsync("/file%252520name.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        upstreamTarget.ShouldBe("/assets/file%2520name.js");
    }

    [Fact]
    public async Task Cdn_client_name_collision_does_not_change_cdn_or_proxy_redirect_behavior()
    {
        var requests = new ConcurrentQueue<string>();
        await using var assets = await StartAssetServer(async context =>
        {
            var path = context.Request.Path.Value!;
            requests.Enqueue(path);
            if (path == "/index.html")
            {
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers.Location = "/actual.html";
            }
            else if (path == "/assets/start")
            {
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers.Location = "/assets/next";
            }
            else
            {
                await context.Response.WriteAsync("cdn index");
            }
        });
        Bff.OnConfigureBffOptions += options =>
            options.StaticAssetsClientName = Constants.HttpClientNames.StaticAssetsProxyClientName;

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend(BffFrontendName.Parse("cdn"))
            .WithCdnIndexHtmlUrl(new Uri(new Uri(assets.Urls.Single()), "/index.html"))
            .MapToPath("/cdn"));
        AddOrUpdateFrontend(Some.BffFrontend(BffFrontendName.Parse("proxy"))
            .WithProxiedStaticAssets(AssetBase(assets))
            .MapToPath("/proxy"));

        using var cdnResponse = await Bff.BrowserClient.GetAsync("/cdn/page");
        using var proxyResponse = await Bff.BrowserClient.GetAsync("/proxy/start");

        cdnResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cdnResponse.Content.ReadAsStringAsync()).ShouldBe("cdn index");
        proxyResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        requests.ToArray().ShouldBe(["/index.html", "/actual.html", "/assets/start"]);
        Bff.Logger.LogEntries.ShouldContain(entry => entry.Contains("StaticAssetsClientName", StringComparison.Ordinal));
    }

    private static Uri AssetBase(WebApplication assets) =>
        new(new Uri(assets.Urls.Single()), "/assets/");

    private static async Task<WebApplication> StartAssetServer(RequestDelegate respond)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        _ = builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        app.Run(respond);
        await app.StartAsync();
        return app;
    }
}
