// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
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

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            Proxy = new BffProxy()
            {
                RemoteApis =
                [
                    new RemoteApi()
                    {
                        LocalPath = The.Path,
                        TargetUri = Api.Url(),
                        RequiredTokenType = requiredTokenType
                    }
                ]
            }
        });

        await Bff.BrowserClient.Login();

        ApiCallDetails result = await Bff.BrowserClient.CallBffHostApi(The.SubPath);
        result.Sub.ShouldBe(The.Sub);
    }

    [Theory]
    [InlineData(RequiredTokenType.Client)]
    [InlineData(RequiredTokenType.UserOrNone)]
    [InlineData(RequiredTokenType.UserOrClient)]
    public async Task When_not_logged_in_can_get_token(RequiredTokenType requiredTokenType)
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            Proxy = new BffProxy()
            {
                RemoteApis =
                [
                    new RemoteApi()
                    {
                        LocalPath = The.Path,
                        TargetUri = Api.Url(),
                        RequiredTokenType = requiredTokenType
                    }
                ]
            }
        });

        ApiCallDetails result = await Bff.BrowserClient.CallBffHostApi(The.SubPath);
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
    public async Task When_not_logged_in_cannot_get_required_user_token()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            Proxy = new BffProxy()
            {
                RemoteApis =
                [
                    new RemoteApi()
                    {
                        LocalPath = The.Path,
                        TargetUri = Api.Url(),
                        RequiredTokenType = RequiredTokenType.User
                    }
                ]
            }
        });

        await Bff.BrowserClient.CallBffHostApi(The.SubPath,
            expectedStatusCode: HttpStatusCode.Unauthorized);


    }
}
