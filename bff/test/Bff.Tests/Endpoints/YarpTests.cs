// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Duende.Bff.Tests.Endpoints;

public class YarpTests : BffTestBase
{
    public YarpTests() : base() =>
        Bff.OnConfigureApp += app =>
        {
            _ = app.MapReverseProxy(proxyApp =>
            {
                _ = proxyApp.UseAntiforgeryCheck();
            });
        };

    private void ConfigureYarp(RouteConfig routeConfig, ClusterConfig? clusterConfig = null) =>
        Bff.OnConfigureBff += bff =>
        {
            _ = bff.AddYarpConfig([routeConfig], [clusterConfig ?? Some.ClusterConfig(Api)]);
        };

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task anonymous_call_with_no_csrf_header_to_no_token_requirement_no_csrf_route_should_succeed(
        BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig());

        await ConfigureBff(setup);


        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.OK
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task anonymous_call_with_no_csrf_header_to_csrf_route_should_fail(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAntiforgeryCheck());
        await ConfigureBff(setup);

        var req = new HttpRequestMessage(HttpMethod.Get, The.PathAndSubPath);
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task can_disable_anti_forgery_check(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAntiforgeryCheck());

        await ConfigureBff(setup);

        Bff.BffOptions.DisableAntiForgeryCheck = (c) => true;

        var req = new HttpRequestMessage(HttpMethod.Get, The.PathAndSubPath);
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task anonymous_call_to_no_token_requirement_route_should_succeed(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig());

        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.OK
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task anonymous_call_to_user_token_requirement_route_should_fail(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task anonymous_call_to_optional_user_token_route_should_succeed(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.UserOrNone));

        await ConfigureBff(setup);

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.OK
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task old_anonymous_call_to_optional_user_token_route_should_succeed(BffSetupType setup)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        ConfigureYarp(Some.RouteConfig().WithOptionalUserAccessToken());
#pragma warning restore CS0618 // Type or member is obsolete

        await ConfigureBff(setup);

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.OK
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_GET_should_forward_user_to_api_for_user(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_PUT_should_forward_user_to_api_for_UserOrNone(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.UserOrNone)
        );

        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            method: HttpMethod.Put
        );

        apiResult.Method.ShouldBe(HttpMethod.Put);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_Post_should_forward_user_to_api_for_User(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));
        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            method: HttpMethod.Post
        );

        apiResult.Method.ShouldBe(HttpMethod.Post);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_Post_should_forward_user_to_api_for_UserOrNone(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.UserOrNone));
        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            method: HttpMethod.Post
        );

        apiResult.Method.ShouldBe(HttpMethod.Post);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task call_to_client_token_route_should_forward_client_token_to_api(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.Client));
        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task call_to_user_or_client_token_route_should_forward_user_or_client_token_to_api(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.UserOrClient));
        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    // Note, path based routing is only available for multi-frontend mode, becuase
    // it relies on the path property on the frontend. 
    [Fact]
    public async Task yarp_works_with_path_based_routing()
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.UserOrClient));
        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = "/somepath"
            },
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration
        });

        AddOrUpdateFrontend(frontEnd);

        _ = await Bff.BrowserClient.Login("/somepath");
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: "/somepath" + The.PathAndSubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.PathAndSubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task response_status_401_from_remote_endpoint_should_return_401_from_bff(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(setup);
        Api.ApiStatusCodeToReturn = HttpStatusCode.Unauthorized;
        _ = await Bff.BrowserClient.Login();

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task response_status_403_from_remote_endpoint_should_return_403_from_bff(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(setup);
        Api.ApiStatusCodeToReturn = HttpStatusCode.Forbidden;
        _ = await Bff.BrowserClient.Login();

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task anonymous_call_with_user_token_requirement_on_cluster_should_fail(BffSetupType setup)
    {
        // Token type set on cluster only, NOT on the route
        ConfigureYarp(
            Some.RouteConfig(),
            Some.ClusterConfig(Api).WithAccessToken(RequiredTokenType.User)
        );

        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_call_with_user_token_requirement_on_cluster_should_succeed(BffSetupType setup)
    {
        // Token type set on cluster only, NOT on the route
        ConfigureYarp(
            Some.RouteConfig(),
            Some.ClusterConfig(Api).WithAccessToken(RequiredTokenType.User)
        );

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_call_to_route_with_token_metadata_should_not_forward_cookie_header_to_api(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        var apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.ApiResponse.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_call_to_route_without_token_metadata_should_not_forward_cookie_header_to_api(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig());

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        var apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.ApiResponse.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_opted_out_cookie_header_is_forwarded_to_api(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig());
        Bff.OnConfigureServices += services =>
            services.Configure<BffOptions>(options => options.RemoveCookieHeaderFromYarpRequests = false);

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        var apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.ApiResponse.RequestHeaders.Keys.ShouldContain("Cookie");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task authenticated_call_should_not_forward_set_cookie_or_clear_site_data_from_api_to_browser(BffSetupType setup)
    {
        Api.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        Api.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";
        Api.ResponseHeadersToReturn["x-from-api"] = "kept";
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        var (response, _) = await Bff.BrowserClient.CallBffHostApi(path: The.PathAndSubPath);

        var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        setCookieValues.ShouldNotContain(value => value.StartsWith("from-api=", StringComparison.Ordinal));
        response.Headers.Contains("Clear-Site-Data").ShouldBeFalse();
        response.Headers.GetValues("x-from-api").ShouldBe(["kept"]);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_opted_out_set_cookie_and_clear_site_data_from_api_are_forwarded_to_browser(BffSetupType setup)
    {
        Api.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        Api.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";
        ConfigureYarp(Some.RouteConfig());
        Bff.OnConfigureServices += services =>
            services.Configure<BffOptions>(options => options.RemoveCookieHeadersFromProxiedResponses = false);

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        var (response, _) = await Bff.BrowserClient.CallBffHostApi(path: The.PathAndSubPath);

        response.Headers.GetValues("Set-Cookie").ShouldContain("from-api=1; path=/");
        response.Headers.GetValues("Clear-Site-Data").ShouldBe(["\"cookies\""]);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_response_header_copy_is_disabled_a_bff_cookie_equal_to_the_api_cookie_reaches_the_browser(BffSetupType setup)
    {
        Api.ResponseHeadersToReturn["Set-Cookie"] = "affinity=1; path=/";
        ConfigureYarp(Some.RouteConfig() with
        {
            Transforms = [new Dictionary<string, string> { ["ResponseHeadersCopy"] = "false" }]
        });
        Bff.OnConfigureApp += app => app.Use((context, next) =>
        {
            context.Response.Headers.Append("Set-Cookie", "affinity=1; path=/");
            return next();
        });

        await ConfigureBff(setup);

        var (response, _) = await Bff.BrowserClient.CallBffHostApi(path: The.PathAndSubPath);

        response.Headers.GetValues("Set-Cookie").ShouldBe(["affinity=1; path=/"]);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_set_cookie_is_in_the_response_header_allow_list_the_api_cookie_is_still_removed(BffSetupType setup)
    {
        Api.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
        Api.ResponseHeadersToReturn["x-from-api"] = "kept";
        ConfigureYarp(Some.RouteConfig() with
        {
            Transforms = [new Dictionary<string, string> { ["ResponseHeadersAllowed"] = "Set-Cookie;x-from-api" }]
        });

        await ConfigureBff(setup);

        var (response, _) = await Bff.BrowserClient.CallBffHostApi(path: The.PathAndSubPath);

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        response.Headers.GetValues("x-from-api").ShouldBe(["kept"]);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task route_config_transform_setting_cookie_header_is_still_removed(BffSetupType setup)
    {
        var routeConfig = Some.RouteConfig() with
        {
            Transforms = new[]
            {
                new Dictionary<string, string>
                {
                    ["RequestHeader"] = "Cookie",
                    ["Set"] = "from-route-config=1",
                }
            }
        };
        ConfigureYarp(routeConfig);

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        var apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.ApiResponse.RequestHeaders.Keys.ShouldNotContain("Cookie");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task transform_registered_after_add_bff_extensions_can_reintroduce_cookie_header(BffSetupType setup)
    {
        Bff.OnConfigureBff += bff =>
        {
            _ = bff.AddYarpConfig([Some.RouteConfig()], [Some.ClusterConfig(Api)])
                .AddTransforms(context => context.AddRequestTransform(transformContext =>
                {
                    transformContext.ProxyRequest.Headers.Add("Cookie", "from-later-transform=1");
                    return default;
                }));
        };

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        var apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.ApiResponse.RequestHeaders.Keys.ShouldContain("Cookie");
    }
}
