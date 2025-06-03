// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Xunit.Abstractions;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Tests.Endpoints;

public class YarpTests : BffTestBase
{
    public YarpTests(ITestOutputHelper output) : base(output) => Bff.OnConfigureEndpoints += endpoints =>
                                                                      {
                                                                          endpoints.MapReverseProxy(proxyApp => { proxyApp.UseAntiforgeryCheck(); });
                                                                      };


    [Fact]
    public async Task anonymous_call_with_no_csrf_header_to_no_token_requirement_no_csrf_route_should_succeed()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig());

        await InitializeAsync();

        await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath,
            expectedStatusCode: HttpStatusCode.OK
        );
    }

    private void ConfigureYarp(RouteConfig routeConfig) =>
        Bff.OnConfigureBff += bff =>
        {
            bff.AddYarpConfig([routeConfig], [Some.ClusterConfig(Api)]);
        };


    [Fact]
    public async Task anonymous_call_with_no_csrf_header_to_csrf_route_should_fail()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAntiforgeryCheck());

        await InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, The.SubPath);
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task can_disable_anti_forgery_check()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        Bff.SetBffOptions += options =>
        {
            options.DisableAntiForgeryCheck = (c) => true;
        };

        ConfigureYarp(Some.RouteConfig()
            .WithAntiforgeryCheck());

        await InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, The.SubPath);
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task anonymous_call_to_no_token_requirement_route_should_succeed()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig());
        await InitializeAsync();

        await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath,
            expectedStatusCode: HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task anonymous_call_to_user_token_requirement_route_should_fail()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.User));

        await InitializeAsync();

        await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task anonymous_call_to_optional_user_token_route_should_succeed()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.UserOrNone));

        await InitializeAsync();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath,
            expectedStatusCode: HttpStatusCode.OK
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBeNull();
    }

    [Fact]
    public async Task old_anonymous_call_to_optional_user_token_route_should_succeed()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

#pragma warning disable CS0618 // Type or member is obsolete
        ConfigureYarp(Some.RouteConfig()
            .WithOptionalUserAccessToken());
#pragma warning restore CS0618 // Type or member is obsolete

        await InitializeAsync();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath,
            expectedStatusCode: HttpStatusCode.OK
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBeNull();
    }

    [Theory]
    [InlineData(RequiredTokenType.User)]
    [InlineData(RequiredTokenType.UserOrNone)]
    public async Task authenticated_GET_should_forward_user_to_api(RequiredTokenType requiredTokenType)
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(requiredTokenType)
        );

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [InlineData(RequiredTokenType.User)]
    [InlineData(RequiredTokenType.UserOrNone)]
    public async Task authenticated_PUT_should_forward_user_to_api(RequiredTokenType requiredTokenType)
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(requiredTokenType)
        );
        await InitializeAsync();
        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath
            , method: HttpMethod.Put
        );

        apiResult.Method.ShouldBe(HttpMethod.Put); ;
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory]
    [InlineData(RequiredTokenType.User)]
    [InlineData(RequiredTokenType.UserOrNone)]
    public async Task authenticated_Post_should_forward_user_to_api(RequiredTokenType requiredTokenType)
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(requiredTokenType)
        );
        await InitializeAsync();
        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath
            , method: HttpMethod.Post
        );

        apiResult.Method.ShouldBe(HttpMethod.Post);
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Fact]
    public async Task call_to_client_token_route_should_forward_client_token_to_api()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.Client));

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Fact]
    public async Task call_to_user_or_client_token_route_should_forward_user_or_client_token_to_api()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.UserOrClient));

        await InitializeAsync();
        await Bff.BrowserClient.Login();
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Fact]
    public async Task yarp_works_with_path_based_routing()
    {
        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.UserOrClient));

        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            SelectionCriteria = new FrontendSelectionCriteria()
            {
                MatchingPath = "/somepath"
            },
        });

        IdentityServer.AddClientFor(frontEnd, Bff.Url());
        Bff.AddOrUpdateFrontend(frontEnd);

        await Bff.BrowserClient.Login("/somepath");
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: "/somepath" + The.SubPath
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.SubPath);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }


    [Fact]
    public async Task response_status_401_from_remote_endpoint_should_return_401_from_bff()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.User));

        Api.ApiStatusCodeToReturn = HttpStatusCode.Unauthorized;

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task response_status_403_from_remote_endpoint_should_return_403_from_bff()
    {
        AddOrUpdateFrontend(Some.BffFrontend());

        ConfigureYarp(Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.User));

        Api.ApiStatusCodeToReturn = HttpStatusCode.Forbidden;

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CallBffHostApi(
            path: The.SubPath,
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

}
