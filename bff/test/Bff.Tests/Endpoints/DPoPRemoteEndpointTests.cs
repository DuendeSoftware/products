// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints;

public class DPoPRemoteEndpointTests(ITestOutputHelper output) : BffTestBase(output)
{
    public override async Task InitializeAsync()
    {
        var idSrvClient = IdentityServer.AddClient(The.ClientId, Bff.Url());

        idSrvClient.RequireDPoP = true;

        Bff.OnConfigureBff += bff => bff.AddRemoteApis();


        await base.InitializeAsync();
        Bff.BffOptions.DPoPJsonWebKey = The.DPoPJsonWebKey;
        Bff.BffOptions.ConfigureOpenIdConnectDefaults = opt =>
        {
            opt.BackchannelHttpHandler = Internet;
            The.DefaultOpenIdConnectConfiguration(opt);
        };
    }

    [Fact]
    public async Task Can_call_dpop_protected_api_with_user_token()
    {
        Api.OnConfigureServices += services =>
        {
            services.ConfigureDPoPTokensForScheme("token");
        };

        Bff.OnConfigureApp += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.User);
        };

        await InitializeAsync();

        await Bff.BrowserClient.Login()
            .CheckHttpStatusCode();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath)
        );

        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        callToApi.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
        callToApi.Sub.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Can_call_dpop_protected_api_with_client_token()
    {
        Api.OnConfigureServices += services =>
        {
            services.ConfigureDPoPTokensForScheme("token");
        };

        Bff.OnConfigureApp += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.Client);
        };

        await InitializeAsync();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath)
        );

        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        callToApi.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
        callToApi.ClientId.ShouldNotBeNullOrEmpty();
    }
}
