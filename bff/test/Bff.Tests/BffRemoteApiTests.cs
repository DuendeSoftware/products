// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Xunit.Abstractions;
using Resource = Duende.Bff.AccessTokenManagement.Resource;

namespace Duende.Bff.Tests;

public class BffRemoteApiTests : BffTestBase
{
    public BffRemoteApiTests(ITestOutputHelper output) : base(output) =>
        Bff.OnConfigureBff += bff =>
        {
            bff.AddRemoteApis();
        };

    [Theory]
    [InlineData(RequiredTokenType.User)]
    [InlineData(RequiredTokenType.UserOrNone)]
    [InlineData(RequiredTokenType.UserOrClient)]
    public async Task When_logged_in_can_proxy_and_get_subject(RequiredTokenType requiredTokenType)
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend()
            .WithRemoteApis(
                    new RemoteApi()
                    {
                        PathMatch = The.Path,
                        TargetUri = Api.Url(),
                        RequiredTokenType = requiredTokenType
                    })
        );

        await Bff.BrowserClient.Login();

        ApiCallDetails result = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        result.Sub.ShouldBe(The.Sub);
    }

    [Theory]
    [InlineData(RequiredTokenType.Client)]
    [InlineData(RequiredTokenType.UserOrNone)]
    [InlineData(RequiredTokenType.UserOrClient)]
    public async Task When_not_logged_in_can_get_token(RequiredTokenType requiredTokenType)
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend()
            .WithRemoteApis(
                new RemoteApi()
                {
                    PathMatch = The.Path,
                    TargetUri = Api.Url(),
                    RequiredTokenType = requiredTokenType
                })
        );

        ApiCallDetails result = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        result.Sub.ShouldBeNull();

        if (requiredTokenType == RequiredTokenType.UserOrClient || requiredTokenType == RequiredTokenType.Client)
        {
            result.ClientId.ShouldBe(The.ClientId);
        }
        else
        {
            result.ClientId.ShouldBeNull();
        }

    }

    [Fact]
    public async Task
        calls_to_remote_endpoint_with_useraccesstokenparameters_having_stored_named_token_should_forward_user_to_api()
    {

        AddOrUpdateFrontend(Some.BffFrontend()
            .WithRemoteApis(Some.RemoteApi() with
            {
                TargetUri = Api.Url(),
                Parameters = new BffUserAccessTokenParameters
                {
                    SignInScheme = Some.BffFrontend().CookieSchemeName,
                    ForceRenewal = true,
                    Resource = Resource.Parse("named_token_stored")
                }
            }));

        Bff.OnConfigureBff += bff =>
        {
            // The remote api registers the testtokenretriever
            bff.Services.AddSingleton<TestTokenRetriever>();
        };

        await InitializeAsync();

        await Bff.BrowserClient.Login();

        var (response, apiResult) = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.ClientId.ShouldBeNull();

        Bff.Resolve<TestTokenRetriever>()
            .UsedContext.ShouldNotBeNull()
            .UserTokenRequestParameters.ShouldNotBeNull()
            .Resource.ShouldBe(Resource.Parse("named_token_stored"));
    }

    [Fact]
    public async Task When_not_logged_in_cannot_get_required_user_token()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend()
            .WithRemoteApis(
                new RemoteApi()
                {
                    PathMatch = The.Path,
                    TargetUri = Api.Url(),
                    RequiredTokenType = RequiredTokenType.User
                })
        );

        await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized);
    }

    private class FakeUserManager : IUserTokenManager
    {

        public bool WasCalled = false;

        public Task<TokenResult<UserToken>> GetAccessTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null,
            CancellationToken ct = new CancellationToken())
        {
            WasCalled = true;
            // We don't care actually about the result token. Just if it was called or not. 
            return Task.FromResult<TokenResult<UserToken>>(TokenResult.Failure("no-token"));
        }

        public Task RevokeRefreshTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null,
            CancellationToken ct = new CancellationToken()) => throw new NotImplementedException();
    }

    [Fact]
    public async Task When_not_logged_in_and_retrieving_optional_user_token_then_no_call_to_IUserTokenManager_should_be_made()
    {
        // Register a fake usermanager, that tracks if it was called
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton<IUserTokenManager, FakeUserManager>();
        };

        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend()
            .WithRemoteApis(
                new RemoteApi()
                {
                    PathMatch = The.Path,
                    TargetUri = Api.Url(),
                    RequiredTokenType = RequiredTokenType.UserOrNone
                })
        );


        await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);

        // A user reported an issue that, if an anonymous request is made,
        // that errors were being logged. This assertion ensures that no such errors are no longer logged.
        //https://github.com/orgs/DuendeSoftware/discussions/396#discussioncomment-14936964
        var userTokenManager = (FakeUserManager)Bff.Resolve<IUserTokenManager>();
        userTokenManager.WasCalled.ShouldBeFalse("The fake user token manager should not have been called because there is no currently logged in user.");
    }

    [Fact]
    public async Task When_not_logged_in_UserOrClient_falls_through_to_client_token()
    {
        // Register a fake usermanager, that tracks if it was called
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton<IUserTokenManager, FakeUserManager>();
        };
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend()
            .WithRemoteApis(
                new RemoteApi()
                {
                    PathMatch = The.Path,
                    TargetUri = Api.Url(),
                    RequiredTokenType = RequiredTokenType.UserOrClient
                })
        );

        await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);

        // A user reported an issue that, if an anonymous request is made,
        // that errors were being logged. This assertion ensures that no such errors are no longer logged.
        //https://github.com/orgs/DuendeSoftware/discussions/396#discussioncomment-14936964
        var userTokenManager = (FakeUserManager)Bff.Resolve<IUserTokenManager>();
        userTokenManager.WasCalled.ShouldBeFalse("The fake user token manager should not have been called because there is no currently logged in user.");
    }
}
