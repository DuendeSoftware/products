// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Tests.Endpoints;

public class YarpCookieHeaderTests(ITestOutputHelper output) : YarpBffIntegrationTestBase(output)
{
    [Fact]
    public async Task authenticated_call_to_route_with_token_metadata_should_not_forward_cookie_header_to_api()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_user/test")
        );

        apiResult.Sub.ShouldBe("alice");
        apiResult.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Fact]
    public async Task authenticated_call_to_route_with_optional_user_token_metadata_should_not_forward_cookie_header_to_api()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_optional_user/test")
        );

        apiResult.Sub.ShouldBe("alice");
        apiResult.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Fact]
    public async Task authenticated_call_to_route_without_token_metadata_should_not_forward_cookie_header_to_api()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        apiResult.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Fact]
    public async Task route_config_transform_setting_cookie_header_is_still_removed()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_cookie_transform/test")
        );

        apiResult.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }
}

public class YarpCookieHeaderOptOutTests : YarpBffIntegrationTestBase
{
    public YarpCookieHeaderOptOutTests(ITestOutputHelper output) : base(output) =>
        YarpBasedBffHost.OnConfigureServices += services =>
            services.Configure<BffOptions>(options => options.RemoveCookieHeaderFromYarpRequests = false);

    [Fact]
    public async Task when_opted_out_cookie_header_is_forwarded_to_api()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        apiResult.RequestHeaders.Keys.ShouldContain("Cookie");
    }
}

public class YarpCookieHeaderLaterTransformTests : YarpBffIntegrationTestBase
{
    // Registered the same way IReverseProxyBuilder.AddTransforms<T>() does, after YarpBffHost
    // calls AddBffExtensions, so this transform provider runs later. (Calling AddReverseProxy()
    // a second time isn't supported by YARP.)
    public YarpCookieHeaderLaterTransformTests(ITestOutputHelper output) : base(output) =>
        YarpBasedBffHost.OnConfigureServices += services =>
            services.AddSingleton<ITransformProvider, AddCookieHeaderTransformProvider>();

    private sealed class AddCookieHeaderTransformProvider : ITransformProvider
    {
        public void ValidateRoute(TransformRouteValidationContext context)
        {
        }

        public void ValidateCluster(TransformClusterValidationContext context)
        {
        }

        public void Apply(TransformBuilderContext context) =>
            context.AddRequestTransform(transformContext =>
            {
                transformContext.ProxyRequest.Headers.Add("Cookie", "from-later-transform=1");
                return ValueTask.CompletedTask;
            });
    }

    [Fact]
    public async Task transform_registered_after_add_bff_extensions_can_reintroduce_cookie_header()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        apiResult.RequestHeaders.Keys.ShouldContain("Cookie");
    }
}
