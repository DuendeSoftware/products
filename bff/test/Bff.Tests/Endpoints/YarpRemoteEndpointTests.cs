// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints;

public class YarpRemoteEndpointTests(ITestOutputHelper output) : YarpBffIntegrationTestBase(output)
{
    [Fact]
    public async Task anonymous_call_with_no_csrf_header_to_no_token_requirement_no_csrf_route_should_succeed() => await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon_no_csrf/test"),
            expectedStatusCode: HttpStatusCode.OK
        );

    [Fact]
    public async Task anonymous_call_with_no_csrf_header_to_csrf_route_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, YarpBasedBffHost.Url("/api_anon/test"));
        var response = await YarpBasedBffHost.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }


    [Fact]
    public async Task anonymous_call_to_no_token_requirement_route_should_succeed() => await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test"),
            expectedStatusCode: HttpStatusCode.OK
        );

    [Fact]
    public async Task anonymous_call_to_user_token_requirement_route_should_fail() => await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_user/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );

    [Fact]
    public async Task anonymous_call_to_optional_user_token_route_should_succeed()
    {
        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_optional_user/test")
        );

        apiResult.Method.ShouldBe("GET");
        apiResult.Path.ShouldBe("/api_optional_user/test");
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBeNull();
    }

    [Fact]
    public async Task call_to_api_badly_cased_anti_forgery()
    {
        var result = await YarpBasedBffHost.BrowserClient.GetAsync(
            new Uri(YarpBasedBffHost.Url("/api_badly_cased_anti_forgery/test"))
        );

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    }

    [Fact]
    public async Task call_to_api_badly_cased_optional_access_token()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");


        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            YarpBasedBffHost.Url("/api_badly_cased_optional_token/test")
        );

        apiResult.Method.ShouldBe("GET");
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
    }


    [Theory]
    [InlineData("/api_user/test")]
    [InlineData("/api_optional_user/test")]
    public async Task authenticated_GET_should_forward_user_to_api(string route)
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url(route)
        );

        apiResult.Method.ShouldBe("GET");
        apiResult.Path.ShouldBe(route);
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
    }

    [Theory]
    [InlineData("/api_user/test")]
    [InlineData("/api_optional_user/test")]
    public async Task authenticated_PUT_should_forward_user_to_api(string route)
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url(route),
            method: HttpMethod.Put
        );

        apiResult.Method.ShouldBe("PUT");
        apiResult.Path.ShouldBe(route);
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
    }

    [Theory]
    [InlineData("/api_user/test")]
    [InlineData("/api_optional_user/test")]
    public async Task authenticated_POST_should_forward_user_to_api(string route)
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url(route),
            method: HttpMethod.Post
        );

        apiResult.Method.ShouldBe("POST");
        apiResult.Path.ShouldBe(route);
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
    }

    [Fact]
    public async Task call_to_client_token_route_should_forward_client_token_to_api()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_client/test")
        );

        apiResult.Method.ShouldBe("GET");
        apiResult.Path.ShouldBe("/api_client/test");
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBe("spa");
    }

    [Fact]
    public async Task call_to_user_or_client_token_route_should_forward_user_or_client_token_to_api()
    {
        {
            ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
                url: YarpBasedBffHost.Url("/api_user_or_client/test")
            );

            apiResult.Method.ShouldBe("GET");
            apiResult.Path.ShouldBe("/api_user_or_client/test");
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBe("spa");
        }

        {
            await YarpBasedBffHost.BffLoginAsync("alice");

            ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
                url: YarpBasedBffHost.Url("/api_user_or_client/test")
            );

            apiResult.Method.ShouldBe("GET");
            apiResult.Path.ShouldBe("/api_user_or_client/test");
            apiResult.Sub.ShouldBe("alice");
            apiResult.ClientId.ShouldBe("spa");
        }
    }

    [Fact]
    public async Task response_status_401_from_remote_endpoint_should_return_401_from_bff()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");
        ApiHost.ApiStatusCodeToReturn = 401;

        var response = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_user/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task response_status_403_from_remote_endpoint_should_return_403_from_bff()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");
        ApiHost.ApiStatusCodeToReturn = 403;

        var response = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_user/test"),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task invalid_configuration_of_routes_should_return_500()
    {
        var response = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_invalid/test"),
            expectedStatusCode: HttpStatusCode.InternalServerError
        );
    }

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
    public async Task authenticated_call_to_route_without_token_metadata_should_not_forward_cookie_header_to_api()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        apiResult.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Fact]
    public async Task when_opted_out_cookie_header_is_forwarded_to_api()
    {
        YarpBasedBffHost.BffOptions.RemoveCookieHeaderFromYarpRequests = false;
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        apiResult.RequestHeaders.Keys.ShouldContain("Cookie");
    }

    [Fact]
    public async Task route_config_transform_setting_cookie_header_is_still_removed()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_cookie_route_transform/test")
        );

        apiResult.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Fact]
    public async Task transform_registered_after_add_bff_extensions_can_reintroduce_cookie_header()
    {
        await YarpBasedBffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_cookie_later_transform/test")
        );

        apiResult.RequestHeaders.Keys.ShouldContain("Cookie");
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
    public async Task when_opted_out_set_cookie_and_clear_site_data_from_api_are_forwarded_to_browser()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        ApiHost.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";
        YarpBasedBffHost.BffOptions.RemoveCookieHeadersFromProxiedResponses = false;
        await YarpBasedBffHost.BffLoginAsync("alice");

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_anon/test")
        );

        response.Headers.GetValues("Set-Cookie").ShouldContain("from-api=1; path=/");
        response.Headers.GetValues("Clear-Site-Data").ShouldBe(["\"cookies\""]);
    }

    [Fact]
    public async Task when_response_header_copy_is_disabled_a_bff_cookie_equal_to_the_api_cookie_reaches_the_browser()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "affinity=1; path=/";

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_response_headers_copy_disabled/test")
        );

        response.Headers.GetValues("Set-Cookie").ShouldBe(["affinity=1; path=/"]);
    }

    [Fact]
    public async Task yarp_route_should_keep_a_bff_written_cookie_while_removing_the_api_cookie()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        await YarpBasedBffHost.BffLoginAsync("alice");

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_user/test")
        );

        var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        setCookieValues.ShouldNotContain(value => value.StartsWith("from-api=", StringComparison.Ordinal));
        setCookieValues.ShouldContain(value => value.StartsWith("bff-written=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task when_set_cookie_is_in_the_response_header_allow_list_the_api_cookie_is_still_removed()
    {
        ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        ApiHost.ResponseHeadersToReturn["x-from-api"] = "kept";

        var (response, _) = await YarpBasedBffHost.BrowserClient.CallBffHostApi(
            url: YarpBasedBffHost.Url("/api_response_headers_allowed/test")
        );

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        response.Headers.GetValues("x-from-api").ShouldBe(["kept"]);
    }
}
