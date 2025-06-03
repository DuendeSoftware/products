// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints;

public class DPoPTestsWithManualAuthentication : BffTestBase, IAsyncLifetime
{
    public DPoPTestsWithManualAuthentication(ITestOutputHelper output) : base(output)
    {
        Bff.EnableBackChannelHandler = false;
        var idSrvClient = IdentityServer.AddClient(The.ClientId, Bff.Url());

        idSrvClient.RequireDPoP = true;

        Bff.SetBffOptions += options =>
        {
            options.DPoPJsonWebKey = The.DPoPJsonWebKey;
        };

        Bff.OnConfigureServices += services =>
        {
            services.AddAuthentication(opt =>
                {
                    opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    opt.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    opt.DefaultAuthenticateScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddOpenIdConnect(opt =>
                {
                    The.DefaultOpenIdConnectConfiguration(opt);
                    opt.BackchannelHttpHandler = Internet;
                });
        };

        Bff.OnConfigureBff += bff => bff.AddRemoteApis();
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapRemoteBffApiEndpoint(The.Path, Api.Url().ToString())
                .WithAccessToken(RequiredTokenType.Client)
                ;
        };

    }

    [Fact]
    public async Task When_logging_in_then_dpop_is_sent() =>
        //// Can login with dpop doesn't work
        await Bff.BrowserClient.Login()
            .CheckHttpStatusCode();

    [Fact]
    public async Task When_calling_api_endpoint_with_dpop_enabled_then_dpop_headers_are_sent()
    {

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.SubPath)
        );

        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        callToApi.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
    }
}
