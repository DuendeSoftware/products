// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Xunit.Abstractions;

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
                        LocalPath = The.Path,
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
                    LocalPath = The.Path,
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
                    LocalPath = The.Path,
                    TargetUri = Api.Url(),
                    RequiredTokenType = RequiredTokenType.User
                })
        );

        await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized);


    }
}
