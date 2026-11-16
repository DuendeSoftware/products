// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class StaticAssetsProxyTests : BffTestBase
{
    public StaticAssetsProxyTests(ITestOutputHelper output) : base(output) => Bff.MapGetForRoot = false;

    [Theory]
    [InlineData("/http%3A%2F%2Fattacker.test/secret.png")]
    [InlineData("/%252e%252e/secret.png")]
    [InlineData("/%2f%2fattacker.test/secret.png")]
    [InlineData("/assets/%255csecret.png")]
    public async Task Encoded_traversal_and_separators_are_rejected_without_fetch_or_fallback(string path)
    {
        var requests = 0;
        Cdn.OnConfigureApp += app => app.MapGet("/{**path}", (HttpContext context) =>
        {
            _ = Interlocked.Increment(ref requests);
            return context.Response.WriteAsync("asset");
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/")));

        using var response = await Bff.BrowserClient.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        requests.ShouldBe(0);
    }

    [Fact]
    public async Task Encoded_traversal_is_rejected_after_frontend_path_mapping()
    {
        var requests = 0;
        Cdn.OnConfigureApp += app => app.MapGet("/{**path}", (HttpContext context) =>
        {
            _ = Interlocked.Increment(ref requests);
            return context.Response.WriteAsync("asset");
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend()
            .WithProxiedStaticAssets(Cdn.Url("/assets/"))
            .MapToPath("/mapped"));

        using var response = await Bff.BrowserClient.GetAsync("/mapped/%252e%252e/secret.png");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        requests.ShouldBe(0);
    }

    [Fact]
    public async Task Root_request_preserves_the_configured_assets_query()
    {
        Cdn.OnConfigureApp += app => app.MapGet("/assets/", (HttpContext context) =>
        {
            context.Response.ContentType = "text/html";
            return context.Response.WriteAsync(context.Request.QueryString.Value ?? string.Empty);
        });
        Bff.OnConfigureServices += services =>
            services.AddSingleton<IIndexHtmlTransformer, BffFrontendIndexTests.TestIndexHtmlTransformer>();

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/?source=base")));

        using var response = await Bff.BrowserClient.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("?source=base - transformed 1");
    }

    [Theory]
    [InlineData("/asset")]
    [InlineData("/missing")]
    public async Task Upstream_site_headers_are_filtered_without_removing_bff_headers(string path)
    {
        Cdn.OnConfigureApp += app =>
        {
            _ = app.MapGet("/assets/asset", WriteUpstreamHeaders);
            _ = app.MapGet("/assets/", WriteUpstreamHeaders);
        };
        Bff.OnConfigureServices += services =>
            services.AddSingleton<IStartupFilter, BffGeneratedHeadersStartupFilter>();

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/")));

        using var response = await Bff.BrowserClient.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.GetValues("Set-Cookie").ShouldBe(["bff-session=kept; Path=/"]);
        response.Headers.GetValues("Clear-Site-Data").ShouldBe(["\"storage\""]);
        response.Headers.Location.ShouldBeNull();
        response.Headers.GetValues("X-Asset-Revision").ShouldBe(["revision-42"]);
        (await response.Content.ReadAsStringAsync()).ShouldBe("asset-v1");
    }

    [Fact]
    public async Task Disabling_site_header_filter_forwards_upstream_headers_without_overwriting_bff_headers()
    {
        Cdn.OnConfigureApp += app => app.MapGet("/assets/asset", async context =>
        {
            context.Response.Headers.TransferEncoding = "chunked";
            await WriteUpstreamHeaders(context);
        });
        Bff.OnConfigureBffOptions += options => options.RemoveCookieHeadersFromProxiedResponses = false;
        Bff.OnConfigureServices += services =>
            services.AddSingleton<IStartupFilter, BffGeneratedHeadersStartupFilter>();

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/")));

        using var response = await Bff.BrowserClient.GetAsync("/asset");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        cookies.ShouldContain("bff-session=kept; Path=/");
        cookies.ShouldContain("upstream=discard; Path=/");
        var clearSiteData = response.Headers.GetValues("Clear-Site-Data").ToArray();
        clearSiteData.ShouldContain("\"storage\"");
        clearSiteData.ShouldContain("\"cache\"");
        response.Headers.Location.ShouldBeNull();
        response.Headers.Contains("Transfer-Encoding").ShouldBeFalse();
    }

    [Fact]
    public async Task Replacement_static_files_client_does_not_depend_on_the_builtin_proxy_transport()
    {
        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton<IStaticFilesClient, ReplacementStaticFilesClient>();
            _ = services.AddSingleton<IHttpClientFactory, FailingHttpClientFactory>();
        };

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/")));

        using var response = await Bff.BrowserClient.GetAsync("/asset");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("replacement");
    }

    [Fact]
    public async Task Same_origin_location_on_a_non_redirect_response_is_preserved()
    {
        Cdn.OnConfigureApp += app => app.MapGet("/assets/asset", (HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.Headers.Location = "/assets/next";
            return Task.CompletedTask;
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/")));

        using var response = await Bff.BrowserClient.GetAsync("/asset");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location?.OriginalString.ShouldBe("/assets/next");
    }

    [Fact]
    public async Task A_non_redirect_response_does_not_forward_multiple_location_values()
    {
        Cdn.OnConfigureApp += app => app.MapGet("/assets/asset", (HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.Headers.Append("Location", "/assets/next");
            context.Response.Headers.Append("Location", "https://other.test/secret");
            return Task.CompletedTask;
        });

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/")));

        using var response = await Bff.BrowserClient.GetAsync("/asset");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Contains("Location").ShouldBeFalse();
    }

    [Theory]
    [InlineData(ClientRegistration.BeforeAddBff)]
    [InlineData(ClientRegistration.AfterAddBff)]
    public async Task Proxy_client_customization_works_before_and_after_add_bff(ClientRegistration registration)
    {
        Bff.EnableStaticAssetsProxyHandler = false;
        Action<IServiceCollection> configure = services =>
            services.AddHttpClient(Constants.HttpClientNames.StaticAssetsProxyClientName,
                    client => client.DefaultRequestHeaders.Add("X-Proxy-Client", "configured"))
                .ConfigurePrimaryHttpMessageHandler(() => Internet);

        if (registration == ClientRegistration.BeforeAddBff)
        {
            Bff.OnConfigureServices += configure;
        }
        else
        {
            Bff.OnConfigureBff += builder => configure(builder.Services);
        }

        Cdn.OnConfigureApp += app => app.MapGet("/assets/asset", (HttpContext context) =>
            context.Response.WriteAsync(context.Request.Headers["X-Proxy-Client"].ToString()));

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/assets/")));

        using var response = await Bff.BrowserClient.GetAsync("/asset");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("configured");
    }

    [Fact]
    public async Task Cdn_and_proxy_clients_keep_their_own_custom_headers()
    {
        Bff.OnConfigureServices += services =>
            services.AddHttpClient(Constants.HttpClientNames.StaticAssetsClientName,
                client => client.DefaultRequestHeaders.Add("X-Cdn-Client", "cdn"));
        Bff.OnConfigureBff += builder =>
            builder.Services.AddHttpClient(Constants.HttpClientNames.StaticAssetsProxyClientName,
                client => client.DefaultRequestHeaders.Add("X-Proxy-Client", "proxy"));
        Cdn.OnConfigureApp += app =>
        {
            _ = app.MapGet("/recorded-index", WriteClientHeaders);
            _ = app.MapGet("/assets/asset", WriteClientHeaders);
        };

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend(BffFrontendName.Parse("cdn"))
            .WithCdnIndexHtmlUrl(Cdn.Url("/recorded-index"))
            .MapToPath("/cdn"));
        AddOrUpdateFrontend(Some.BffFrontend(BffFrontendName.Parse("proxy"))
            .WithProxiedStaticAssets(Cdn.Url("/assets/"))
            .MapToPath("/proxy"));

        using var cdnResponse = await Bff.BrowserClient.GetAsync("/cdn/page");
        using var proxyResponse = await Bff.BrowserClient.GetAsync("/proxy/asset");

        (await cdnResponse.Content.ReadAsStringAsync()).ShouldBe("cdn|");
        (await proxyResponse.Content.ReadAsStringAsync()).ShouldBe("|proxy");
    }

    private static Task WriteClientHeaders(HttpContext context) => context.Response.WriteAsync(
        $"{context.Request.Headers["X-Cdn-Client"]}|{context.Request.Headers["X-Proxy-Client"]}");

    private static async Task WriteUpstreamHeaders(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status201Created;
        context.Response.ContentType = "text/plain";
        context.Response.Headers.SetCookie = "upstream=discard; Path=/";
        context.Response.Headers["Clear-Site-Data"] = "\"cache\"";
        context.Response.Headers.Location = "https://other.test/asset";
        context.Response.Headers["X-Asset-Revision"] = "revision-42";
        await context.Response.WriteAsync("asset-v1");
    }

    public enum ClientRegistration
    {
        BeforeAddBff,
        AfterAddBff
    }

    private sealed class BffGeneratedHeadersStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            _ = app.Use(async (context, nextRequest) =>
            {
                context.Response.Headers.Append("Set-Cookie", "bff-session=kept; Path=/");
                context.Response.Headers.Append("Clear-Site-Data", "\"storage\"");
                await nextRequest();
            });
            next(app);
        };
    }

    private sealed class ReplacementStaticFilesClient : IStaticFilesClient
    {
        public Task<string?> GetIndexHtmlAsync(CT ct = default) => Task.FromResult<string?>(null);

        public Task ProxyStaticAssetsAsync(HttpContext context, CT ct = default) =>
            context.Response.WriteAsync("replacement", ct);
    }

    private sealed class FailingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException($"Unexpected HTTP client '{name}' requested.");
    }
}
