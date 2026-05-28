// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Tests.Endpoints;

public class YarpAccessTokenRetrieverTests : BffTestBase
{
    public YarpAccessTokenRetrieverTests() : base() =>
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
    public async Task yarp_route_with_custom_retriever_should_use_custom_retriever(BffSetupType setup)
    {
        var testRetriever = new TestTokenRetriever();

        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton(testRetriever);
        };

        ConfigureYarp(
            Some.RouteConfig().WithAccessToken(RequiredTokenType.User),
            Some.ClusterConfig(Api).WithAccessTokenRetriever<TestTokenRetriever>()
        );

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        // TestTokenRetriever returns NoAccessTokenResult, so the request goes through without a token
        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.OK
        );

        _ = testRetriever.UsedContext.ShouldNotBeNull("The custom retriever should have been invoked");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_with_custom_retriever_should_populate_context_correctly(BffSetupType setup)
    {
        var testRetriever = new TestTokenRetriever();

        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton(testRetriever);
        };

        ConfigureYarp(
            Some.RouteConfig().WithAccessToken(RequiredTokenType.User),
            Some.ClusterConfig(Api).WithAccessTokenRetriever<TestTokenRetriever>()
        );

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.OK
        );

        var context = testRetriever.UsedContext.ShouldNotBeNull();
        context.Metadata.TokenType.ShouldBe(RequiredTokenType.User);
        context.Metadata.AccessTokenRetriever.ShouldBe(typeof(TestTokenRetriever));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_with_custom_retriever_that_fails_should_return_401(BffSetupType setup)
    {
        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton<FailureAccessTokenRetriever>();
        };

        ConfigureYarp(
            Some.RouteConfig().WithAccessToken(RequiredTokenType.User),
            Some.ClusterConfig(Api).WithAccessTokenRetriever<FailureAccessTokenRetriever>()
        );

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_with_custom_retriever_should_forward_token_to_api(BffSetupType setup)
    {
        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton(new TestAccessTokenRetriever(() => CreateAccessToken("custom-sub", "custom-client")));
        };

        ConfigureYarp(
            Some.RouteConfig().WithAccessToken(RequiredTokenType.User),
            Some.ClusterConfig(Api).WithAccessTokenRetriever<TestAccessTokenRetriever>()
        );

        await ConfigureBff(setup);

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Sub.ShouldBe("custom-sub");
        apiResult.ClientId.ShouldBe("custom-client");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_without_custom_retriever_should_use_default_retriever(BffSetupType setup)
    {
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Fact]
    public void WithAccessTokenRetriever_stores_type_name_in_cluster_metadata()
    {
        var cluster = Some.ClusterConfig(Api)
            .WithAccessTokenRetriever<TestTokenRetriever>();

        var metadata = cluster.Metadata.ShouldNotBeNull();
        metadata.ShouldContainKey(Constants.Yarp.AccessTokenRetrieverMetadata);
        metadata[Constants.Yarp.AccessTokenRetrieverMetadata]
            .ShouldBe(typeof(TestTokenRetriever).AssemblyQualifiedName);
    }

    [Fact]
    public void WithAccessTokenRetriever_preserves_existing_metadata()
    {
        var cluster = Some.ClusterConfig(Api)
            .WithAccessToken(RequiredTokenType.User)
            .WithAccessTokenRetriever<TestTokenRetriever>();

        var metadata = cluster.Metadata.ShouldNotBeNull();
        metadata.ShouldContainKey(Constants.Yarp.TokenTypeMetadata);
        metadata.ShouldContainKey(Constants.Yarp.AccessTokenRetrieverMetadata);
    }

    [Fact]
    public void WithAccessTokenRetriever_first_call_wins()
    {
        var cluster = Some.ClusterConfig(Api)
            .WithAccessTokenRetriever<TestTokenRetriever>()
            .WithAccessTokenRetriever<FailureAccessTokenRetriever>();

        var metadata = cluster.Metadata.ShouldNotBeNull();
        metadata[Constants.Yarp.AccessTokenRetrieverMetadata]
            .ShouldBe(typeof(TestTokenRetriever).AssemblyQualifiedName,
                "TryAdd semantics mean the first call should win");
    }

    [Fact]
    public void WithAccessTokenRetriever_on_null_cluster_config_should_throw()
    {
        ClusterConfig config = null!;

        _ = Should.Throw<ArgumentNullException>(() => config.WithAccessTokenRetriever<TestTokenRetriever>());
    }

    // --- RouteConfig unit tests ---

    [Fact]
    public void WithAccessTokenRetriever_stores_type_name_in_route_metadata()
    {
        var route = Some.RouteConfig()
            .WithAccessTokenRetriever<TestTokenRetriever>();

        var metadata = route.Metadata.ShouldNotBeNull();
        metadata.ShouldContainKey(Constants.Yarp.AccessTokenRetrieverMetadata);
        metadata[Constants.Yarp.AccessTokenRetrieverMetadata]
            .ShouldBe(typeof(TestTokenRetriever).AssemblyQualifiedName);
    }

    [Fact]
    public void WithAccessTokenRetriever_on_route_preserves_existing_metadata()
    {
        var route = Some.RouteConfig()
            .WithAccessToken(RequiredTokenType.User)
            .WithAccessTokenRetriever<TestTokenRetriever>();

        var metadata = route.Metadata.ShouldNotBeNull();
        metadata.ShouldContainKey(Constants.Yarp.TokenTypeMetadata);
        metadata.ShouldContainKey(Constants.Yarp.AccessTokenRetrieverMetadata);
    }

    [Fact]
    public void WithAccessTokenRetriever_on_route_first_call_wins()
    {
        var route = Some.RouteConfig()
            .WithAccessTokenRetriever<TestTokenRetriever>()
            .WithAccessTokenRetriever<FailureAccessTokenRetriever>();

        var metadata = route.Metadata.ShouldNotBeNull();
        metadata[Constants.Yarp.AccessTokenRetrieverMetadata]
            .ShouldBe(typeof(TestTokenRetriever).AssemblyQualifiedName,
                "TryAdd semantics mean the first call should win");
    }

    [Fact]
    public void WithAccessTokenRetriever_on_null_route_config_should_throw()
    {
        RouteConfig config = null!;

        _ = Should.Throw<ArgumentNullException>(() => config.WithAccessTokenRetriever<TestTokenRetriever>());
    }

    // --- RouteConfig integration tests ---

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_with_route_level_custom_retriever_should_use_custom_retriever(BffSetupType setup)
    {
        var testRetriever = new TestTokenRetriever();

        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton(testRetriever);
        };

        ConfigureYarp(
            Some.RouteConfig()
                .WithAccessToken(RequiredTokenType.User)
                .WithAccessTokenRetriever<TestTokenRetriever>()
        );

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.OK
        );

        _ = testRetriever.UsedContext.ShouldNotBeNull("The custom retriever should have been invoked");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_with_route_level_custom_retriever_that_fails_should_return_401(BffSetupType setup)
    {
        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton<FailureAccessTokenRetriever>();
        };

        ConfigureYarp(
            Some.RouteConfig()
                .WithAccessToken(RequiredTokenType.User)
                .WithAccessTokenRetriever<FailureAccessTokenRetriever>()
        );

        await ConfigureBff(setup);
        _ = await Bff.BrowserClient.Login();

        _ = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_with_route_level_custom_retriever_should_forward_token_to_api(BffSetupType setup)
    {
        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton(new TestAccessTokenRetriever(() => CreateAccessToken("route-sub", "route-client")));
        };

        ConfigureYarp(
            Some.RouteConfig()
                .WithAccessToken(RequiredTokenType.User)
                .WithAccessTokenRetriever<TestAccessTokenRetriever>()
        );

        await ConfigureBff(setup);

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Sub.ShouldBe("route-sub");
        apiResult.ClientId.ShouldBe("route-client");
    }

    // --- Precedence test: route metadata wins over cluster metadata ---

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task yarp_route_level_retriever_takes_precedence_over_cluster_level(BffSetupType setup)
    {
        Bff.OnConfigureServices += services =>
        {
            _ = services.AddSingleton(new TestAccessTokenRetriever(() => CreateAccessToken("route-sub", "route-client")));
            _ = services.AddSingleton<FailureAccessTokenRetriever>();
        };

        ConfigureYarp(
            Some.RouteConfig()
                .WithAccessToken(RequiredTokenType.User)
                .WithAccessTokenRetriever<TestAccessTokenRetriever>(),
            Some.ClusterConfig(Api)
                .WithAccessTokenRetriever<FailureAccessTokenRetriever>()
        );

        await ConfigureBff(setup);

        // If route-level wins, TestAccessTokenRetriever returns a valid token → 200
        // If cluster-level wins, FailureAccessTokenRetriever returns error → 401
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            path: The.PathAndSubPath
        );

        apiResult.Sub.ShouldBe("route-sub");
        apiResult.ClientId.ShouldBe("route-client");
    }

    private async Task<AccessTokenResult> CreateAccessToken(string sub, string clientId)
    {
        var tokens = IdentityServer.Resolve<Duende.IdentityServer.Services.ITokenService>();
        var token = new Duende.IdentityServer.Models.Token(Duende.IdentityServer.IdentityServerConstants.TokenTypes.AccessToken)
        {
            Issuer = IdentityServer.Url().ToString().TrimEnd('/'),
            Lifetime = Convert.ToInt32(TimeSpan.FromDays(1).TotalSeconds),
            CreationTime = DateTime.UtcNow,

            Claims = new List<System.Security.Claims.Claim>
            {
                new("client_id", clientId),
                new("sub", sub)
            },
            Audiences = new List<string>
            {
                IdentityServer.Url("/resources").ToString()
            },
            AccessTokenType = Duende.IdentityServer.Models.AccessTokenType.Jwt
        };

        return new BearerTokenResult()
        {
            AccessToken = AccessToken.Parse(await tokens.CreateSecurityTokenAsync(token))
        };
    }
}
