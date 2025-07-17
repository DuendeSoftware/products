// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Microsoft.Extensions.Caching.Hybrid;
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
            IndexHtmlUrl = Cdn.Url("index.html")
        });

        await Bff.BrowserClient.Login()
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
            .WithIndexHtmlUrl(Cdn.Url("index.html"))
            .WithRemoteApis(new RemoteApi()
            {
                TargetUri = Api.Url(),
                LocalPath = The.Path,
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
            IndexHtmlUrl = Cdn.Url("index.html")
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
            IndexHtmlUrl = Cdn.Url("index.html")
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
            IndexHtmlUrl = Cdn.Url("index.html")
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
            IndexHtmlUrl = Cdn.Url("index.html")
        });

        var html = await GetIndexHtml();
        html.ShouldEndWith(" - transformed 1");

        html = await GetIndexHtml();
        html.ShouldEndWith(" - transformed 1");

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            IndexHtmlUrl = Cdn.Url("index2.html")
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

        public Task<string?> Transform(string html, CT ct = default) => Task.FromResult<string?>($"{html} - transformed {count++}");
    }

}
