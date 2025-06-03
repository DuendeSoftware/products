// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestInfra;
using Duende.IdentityServer.Extensions;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class BffWithoutExplicitFrontendTests(ITestOutputHelper output) : BffTestBase(output)
{
    public override async Task InitializeAsync()
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet("/secret", (HttpContext c, CT ct) =>
            {
                if (!c.User.IsAuthenticated())
                {
                    c.Response.StatusCode = 401;
                    return "";
                }

                return "";
            });
        };
        Bff.SetBffOptions += options =>
        {
            options.ConfigureOpenIdConnectDefaults = oidc =>
            {
                oidc.Authority = IdentityServer.Url().ToString();
                oidc.ClientId = DefaultOidcClient.ClientId;
                oidc.ClientSecret = DefaultOidcClient.ClientSecret;
                oidc.ResponseType = DefaultOidcClient.ResponseType;
                oidc.ResponseMode = DefaultOidcClient.ResponseMode;
                oidc.MapInboundClaims = false;
                oidc.GetClaimsFromUserInfoEndpoint = true;
                oidc.SaveTokens = true;
                oidc.BackchannelHttpHandler = Internet;
            };
        };

        await base.InitializeAsync();
    }

    [Fact]
    public async Task Can_login()
    {
        await InitializeAsync();

        await Bff.BrowserClient.Login()
            .CheckHttpStatusCode();

        await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode();

        var cookie = Bff.BrowserClient.Cookies.GetCookies(Bff.Url("/somepath")).FirstOrDefault();
        cookie.ShouldNotBeNull();
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Name.ShouldBe(Constants.Cookies.DefaultCookieName);
        cookie.Secure.ShouldBeTrue();
        cookie.Path.ShouldBe("/");
    }
}
