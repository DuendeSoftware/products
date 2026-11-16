// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using NSubstitute;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class BffFrontendIndexTests : BffTestBase
{
    public BffFrontendIndexTests(ITestOutputHelper output) : base(output) =>
        // Disable the map to '/' for the test
        Bff.MapGetForRoot = false;

    [Fact]
    public async Task After_login_index_document_is_returned()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            CdnIndexHtmlUrl = Cdn.Url("index.html")
        });

        await Bff.BrowserClient.Login()
               .CheckResponseContent(Cdn.IndexHtml);

        // A non-existing page should also return the index.html
        await Bff.BrowserClient.GetAsync("/not-found")
            .CheckResponseContent(Cdn.IndexHtml);

        // The existing image.png should also return index html, because
        // we're not doing proxying of static assets here.
        await Bff.BrowserClient.GetAsync("/image.png")
            .CheckResponseContent(Cdn.IndexHtml);
    }

    [Fact]
    public async Task Given_index_can_call_proxied_endpoint()
    {
        Bff.OnConfigureBff += opt =>
        {
            opt.AddRemoteApis();
        };

        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend()
            .WithCdnIndexHtmlUrl(Cdn.Url("index.html"))
            .WithRemoteApis(new RemoteApi()
            {
                TargetUri = Api.Url(),
                PathMatch = The.Path,
                RequiredTokenType = RequiredTokenType.Client,
            })
        );

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Cdn.IndexHtml);

        var result = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
    }
    [Fact]
    public async Task Given_index_can_call_local_api()
    {
        Bff.OnConfigureApp += app =>
        {
            app.MapGet("/local", () => "ok");
        };

        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            CdnIndexHtmlUrl = Cdn.Url("index.html")
        });

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Cdn.IndexHtml);

        var result = await Bff.BrowserClient.GetAsync("/local")
            .CheckResponseContent("ok");
    }

    [Fact]
    public async Task Index_document_is_returned_on_fallback_path()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            CdnIndexHtmlUrl = Cdn.Url("index.html")
        });

        // get a random path. The index.html should be registered as fallback route
        await Bff.BrowserClient.GetAsync("/random-path")
            .CheckHttpStatusCode()
            .CheckResponseContent(Cdn.IndexHtml);
    }

    [Fact]
    public async Task Can_customize_index_html()
    {
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton<IIndexHtmlTransformer, TestIndexHtmlTransformer>();
        };

        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            CdnIndexHtmlUrl = Cdn.Url("index.html")
        });

        var html = await GetIndexHtml();
        html.ShouldEndWith(" - transformed 1");

    }

    private async Task<string> GetIndexHtml()
    {
        // get a random path. The index.html should be registered as fallback route
        var response = await Bff.BrowserClient.GetAsync("/random-path")
            .CheckHttpStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        return html;
    }

    [Fact]
    public async Task IndexHtml_is_cached_but_refreshed_when_modifying_frontend()
    {
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton<IIndexHtmlTransformer, TestIndexHtmlTransformer>();
            services.AddSingleton<HybridCache, TestHybridCache>();
        };

        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            CdnIndexHtmlUrl = Cdn.Url("index.html")
        });

        var html = await GetIndexHtml();
        html.ShouldEndWith(" - transformed 1");

        html = await GetIndexHtml();
        html.ShouldEndWith(" - transformed 1");

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            CdnIndexHtmlUrl = Cdn.Url("index2.html")
        });
        var cache = (TestHybridCache)Bff.Resolve<HybridCache>();
        cache.WaitUntilRemoveAsyncCalled(TimeSpan.FromSeconds(5));
        // Note, there is a possibility for a race condition because the cache is cleared executed using
        // asynchronously in the background. But because the cache is mocked it's all synchronous.
        // Add synchronization to the test if it starts to become unstable.
        html = await GetIndexHtml();
        html.ShouldEndWith(" - transformed 2");
    }

    public class TestIndexHtmlTransformer : IIndexHtmlTransformer
    {
        private int count = 1;

        public Task<string?> Transform(string html, BffFrontend frontend, CT ct = default) => Task.FromResult<string?>($"{html} - transformed {count++}");
    }

    [Fact]
    public async Task When_proxying_static_assets_then_index_html_is_also_transformed()
    {
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton<IIndexHtmlTransformer, TestIndexHtmlTransformer>();
        };
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            StaticAssetsUrl = Cdn.Url("/")
        });

        await Bff.BrowserClient.GetAsync("/")
            .CheckResponseContent(Cdn.IndexHtml + " - transformed 1");

        // When you get an explicit HTML file, it's not the index.html file, so we're
        // not transforming it
        await Bff.BrowserClient.GetAsync("/index2.html")
            .CheckResponseContent(Cdn.IndexHtml);

        // A non-existing page should also return the index.html and it should go through the transformer
        await Bff.BrowserClient.GetAsync("/not-found")
            .CheckResponseContent(Cdn.IndexHtml + " - transformed 2");

        // The existing image.png should be proxied through the BFF. and should not be transformed
        await Bff.BrowserClient.GetAsync("/image.png")
            .CheckResponseContent(Cdn.ImageBytes);
    }

    [Theory]
    [InlineData("/", "<html><body>The transformed index document contains additional frontend configuration and application content, making it longer than the original document.</body></html>")]
    [InlineData("/not-found", "<html><body>The transformed index document contains additional frontend configuration and application content, making it longer than the original document.</body></html>")]
    [InlineData("/", "<p>short</p>")]
    [InlineData("/not-found", "<p>short</p>")]
    [InlineData("/", "<p>\u00e9 \u6f22 \U0001f600</p>")]
    [InlineData("/not-found", "<p>\u00e9 \u6f22 \U0001f600</p>")]
    [InlineData("/", "")]
    [InlineData("/not-found", "")]
    [InlineData("/", null)]
    [InlineData("/not-found", null)]
    public async Task proxying_transformed_index_html_sets_content_length_to_utf8_byte_count(string path, string? transformedHtml)
    {
        var transformer = Substitute.For<IIndexHtmlTransformer>();
        _ = transformer.Transform(Cdn.IndexHtml, Arg.Any<BffFrontend>(), Arg.Any<CT>())
            .Returns(Task.FromResult(transformedHtml));
        Bff.OnConfigureServices += services => services.AddSingleton(transformer);

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/")));

        using var response = await Bff.BrowserClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead)
            .CheckHttpStatusCode();

        response.Content.Headers.Contains("Content-Length").ShouldBeTrue();
        var contentLength = response.Content.Headers.ContentLength;
        var bytes = await response.Content.ReadAsByteArrayAsync();

        bytes.ShouldBe(Encoding.UTF8.GetBytes(transformedHtml ?? string.Empty));
        contentLength.ShouldBe(bytes.LongLength);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/not-found")]
    public async Task proxying_transformed_index_html_over_kestrel_returns_complete_response(string path)
    {
        var transformedHtml = Cdn.IndexHtml + "<p>Additional frontend configuration: \u00e9 \u6f22 \U0001f600</p>";
        var transformer = Substitute.For<IIndexHtmlTransformer>();
        _ = transformer.Transform(Cdn.IndexHtml, Arg.Any<BffFrontend>(), Arg.Any<CT>())
            .Returns(Task.FromResult<string?>(transformedHtml));

        await InitializeAsync();

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        _ = builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        _ = builder.Logging.AddProvider(new TestLoggerProvider(Context.WriteOutput, "kestrel - "));
        _ = builder.Services.AddAuthentication();
        _ = builder.Services.AddAuthorization();
        _ = builder.Services.AddRouting();
        _ = builder.Services.AddSingleton(transformer);
        _ = builder.Services.AddBff(options => options.BackchannelHttpHandler = Internet)
            .AddFrontends(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/")));
        _ = builder.Services.AddHttpClient(Constants.HttpClientNames.StaticAssetsProxyClientName)
            .ConfigurePrimaryHttpMessageHandler(() => Internet);

        await using var app = builder.Build();
        _ = app.UseRouting();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.UseBff();
        await app.StartAsync();

        // Use a real connection so Kestrel enforces the response's Content-Length.
        using var client = new HttpClient
        {
            BaseAddress = new Uri(app.Urls.Single()),
            Timeout = TimeSpan.FromSeconds(30)
        };
        using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead)
            .CheckHttpStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();

        bytes.ShouldBe(Encoding.UTF8.GetBytes(transformedHtml));
        response.Content.Headers.ContentLength.ShouldBe(bytes.LongLength);
    }

    [Theory]
    [InlineData("/", false)]
    [InlineData("/not-found", false)]
    [InlineData("/index2.html", false)]
    [InlineData("/image.png", false)]
    [InlineData("/index2.html", true)]
    [InlineData("/image.png", true)]
    public async Task proxying_untransformed_static_assets_preserves_content_and_length(string path, bool useTransformer)
    {
        if (useTransformer)
        {
            Bff.OnConfigureServices += services => services.AddSingleton<IIndexHtmlTransformer, TestIndexHtmlTransformer>();
        }

        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend().WithProxiedStaticAssets(Cdn.Url("/")));

        using var response = await Bff.BrowserClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead)
            .CheckHttpStatusCode();

        response.Content.Headers.Contains("Content-Length").ShouldBeTrue();
        var contentLength = response.Content.Headers.ContentLength;
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var expectedBytes = path == "/image.png" ? Cdn.ImageBytes : Encoding.UTF8.GetBytes(Cdn.IndexHtml);

        bytes.ShouldBe(expectedBytes);
        contentLength.ShouldBe(expectedBytes.LongLength);
    }

    [Fact]
    public async Task Can_also_proxy_all_static_assets()
    {
        Bff.OnConfigureApp += app => app.MapGet("/test", () => "test");
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            StaticAssetsUrl = Cdn.Url("/")
        });

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Cdn.IndexHtml);

        await Bff.BrowserClient.GetAsync("/test")
            .CheckResponseContent("test");

        // A non-existing page should also return the index.html
        await Bff.BrowserClient.GetAsync("/not-found")
            .CheckResponseContent(Cdn.IndexHtml);

        // The existing image.png should be proxied through the BFF.
        await Bff.BrowserClient.GetAsync("/image.png")
            .CheckResponseContent(Cdn.ImageBytes);
    }

    [Fact]
    public async Task static_assets_proxying_also_allows_query_strings()
    {
        Cdn.OnConfigureApp += app => app.MapGet("/withQuery",
            ([FromQuery] string? q) => q ?? "no_query");

        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            StaticAssetsUrl = Cdn.Url()
        });

        // Verifying that querystring parameters are passed correctly to the proxied endpoint
        // This is important, because vite dev server adds a querystring parameters to get
        // partial files
        await Bff.BrowserClient.GetAsync("/withQuery?q=abc")
            .CheckResponseContent("abc");

        // Just a quick check to verify encoding works as expected
        await Bff.BrowserClient.GetAsync("/withQuery?q=" + UrlEncoder.Default.Encode("?@%^&*()"))
            .CheckResponseContent("?@%^&*()");
    }

    [Fact]
    public async Task Proxying_static_assets_works_with_path_based_routing()
    {
        Cdn.OnConfigureApp += app => app.MapGet("/some_static", () => "default_frontend");

        await InitializeAsync();

        // Creating a frontend that is mapped to a path.
        AddOrUpdateFrontend(
            Some.BffFrontend(BffFrontendName.Parse("mapped_to_path"))
                .WithProxiedStaticAssets(Cdn.Url())
                .MapToPath(The.Path));

        // Also a default frontend, that has different static content registered
        AddOrUpdateFrontend(Some.BffFrontend()
            .WithCdnIndexHtmlUrl(Cdn.Url("/some_static")));

        // When getting the root of the path-mapped frontend, then we should get the static content
        // from the cdn
        await Bff.BrowserClient.GetAsync(The.Path)
            .CheckResponseContent(Cdn.IndexHtml);

        // It should also work for sub-paths and client side routing (The /test path doesn't exist on the cdn)
        // so the index.html should be returned
        await Bff.BrowserClient.GetAsync(The.Path + "/test")
            .CheckResponseContent(Cdn.IndexHtml);

        // It should also work for static assets that exist on the cdn, such as the image.
        await Bff.BrowserClient.GetAsync(The.Path + "/image.png")
            .CheckResponseContent(Cdn.ImageBytes);

        // Now, if you go to the default frontend, it should return
        // the different static content that's only registered for the default frontend
        await Bff.BrowserClient.GetAsync("/")
            .CheckResponseContent("default_frontend");

        // The image should not be registered (we only proxy the index.html for the default frontend)
        await Bff.BrowserClient.GetAsync("/image.png")
            .CheckResponseContent("default_frontend");
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_using_StaticAssets_func_controls(bool indexHtmlOnly)
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend().WithBffStaticAssets(Cdn.Url("/"), () => indexHtmlOnly));

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Cdn.IndexHtml);

        if (indexHtmlOnly)
        {
            // If we only proxy the index html, then any unmatched route (including the image.png)
            // should return the index.html content (for client side routing purposes)
            await Bff.BrowserClient.GetAsync("/image.png")
                .CheckResponseContent(Cdn.IndexHtml);
        }
        else
        {
            // If we proxy all static assets for this frontend, then the image.png should be proxied
            await Bff.BrowserClient.GetAsync("/image.png")
                .CheckResponseContent(Cdn.ImageBytes);
        }
    }

}
