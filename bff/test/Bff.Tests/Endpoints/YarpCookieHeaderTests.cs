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

    [Fact]
    public async Task authenticated_call_should_not_forward_set_cookie_or_clear_site_data_from_api_to_browser()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        ApiHost.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";
        ApiHost.ResponseHeadersToReturn["x-from-api"] = "kept";

        await YarpBasedBffHost.BffLoginAsync("alice");

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_user/test")
        );

        var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        setCookieValues.ShouldNotContain(value => value.StartsWith("from-api=", StringComparison.Ordinal));
        response.Headers.Contains("Clear-Site-Data").ShouldBeFalse();
        response.Headers.GetValues("x-from-api").ShouldBe(["kept"]);
    }

    [Fact]
    public async Task when_response_headers_copy_is_disabled_the_api_set_cookie_is_not_forwarded_and_the_transform_does_not_throw()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";

        await YarpBasedBffHost.BffLoginAsync("alice");

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_response_headers_copy_disabled/test")
        );

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task when_set_cookie_is_in_the_response_header_allow_list_the_api_cookie_is_still_removed()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        ApiHost.ResponseHeadersToReturn["x-from-api"] = "kept";

        await YarpBasedBffHost.BffLoginAsync("alice");

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_response_headers_allowed/test")
        );

        var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        setCookieValues.ShouldNotContain(value => value.StartsWith("from-api=", StringComparison.Ordinal));
        response.Headers.GetValues("x-from-api").ShouldBe(["kept"]);
    }
}

public class YarpCookieHeaderOptOutTests : YarpBffIntegrationTestBase
{
    public YarpCookieHeaderOptOutTests(ITestOutputHelper output) : base(output) =>
        YarpBasedBffHost.OnConfigureServices += services =>
            services.Configure<BffOptions>(options =>
            {
                options.RemoveCookieHeaderFromYarpRequests = false;
                options.RemoveCookieHeadersFromProxiedResponses = false;
            });

    [Fact]
    public async Task when_opted_out_cookie_header_is_forwarded_to_api()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        apiResult.RequestHeaders.Keys.ShouldContain("Cookie");
    }

    [Fact]
    public async Task when_opted_out_set_cookie_and_clear_site_data_from_api_are_forwarded_to_browser()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        ApiHost.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";

        await YarpBasedBffHost.BffLoginAsync("alice");

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        response.Headers.GetValues("Set-Cookie").ShouldContain("from-api=1; path=/");
        response.Headers.GetValues("Clear-Site-Data").ShouldBe(["\"cookies\""]);
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

public class YarpCookieHeaderBffWrittenCookieTests : YarpBffIntegrationTestBase
{
    // Writes a BFF-side Set-Cookie equal to the API's cookie value, via a response transform
    // registered the same way as the later request-transform provider above. This exercises the
    // `HeadersCopied` guard in ApiCookieResponseHeaderRemoveTransform: on the
    // api_response_headers_copy_disabled route, nothing from the API was copied, so the removal
    // transform must leave this BFF-written cookie alone.
    public YarpCookieHeaderBffWrittenCookieTests(ITestOutputHelper output) : base(output) =>
        YarpBasedBffHost.OnConfigureServices += services =>
            services.Insert(0, ServiceDescriptor.Singleton<ITransformProvider, AddBffCookieTransformProvider>());

    private sealed class AddBffCookieTransformProvider : ITransformProvider
    {
        public void ValidateRoute(TransformRouteValidationContext context)
        {
        }

        public void ValidateCluster(TransformClusterValidationContext context)
        {
        }

        public void Apply(TransformBuilderContext context) =>
            context.AddResponseTransform(responseContext =>
            {
                responseContext.HttpContext.Response.Headers["Set-Cookie"] = "affinity=1; path=/";
                return ValueTask.CompletedTask;
            });
    }

    [Fact]
    public async Task when_response_headers_copy_is_disabled_a_bff_cookie_equal_to_the_api_cookie_reaches_the_browser()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "affinity=1; path=/";

        await YarpBasedBffHost.BffLoginAsync("alice");

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_response_headers_copy_disabled/test")
        );

        response.Headers.GetValues("Set-Cookie").ShouldBe(["affinity=1; path=/"]);
    }
}
