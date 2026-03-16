// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestInfra;
using Duende.IdentityServer.Extensions;
namespace Duende.Bff.Tests;

public class BffWithoutExplicitFrontendTests : BffTestBase
{
    public override async ValueTask InitializeAsync()
    {
        Bff.OnConfigureApp += app =>
        {
            _ = app.MapGet("/secret", (HttpContext c, Ct ct) =>
            {
                if (!c.User.IsAuthenticated())
                {
                    c.Response.StatusCode = 401;
                    return "";
                }

                return "";
            });
        };

        await base.InitializeAsync();

        Bff.BffOptions.ConfigureOpenIdConnectDefaults = oidc =>
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
    }

    [Fact]
    public async Task Can_login()
    {
        await InitializeAsync();

        _ = await Bff.BrowserClient.Login()
            .CheckHttpStatusCode();

        _ = await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode();

        var cookie = Bff.BrowserClient.Cookies.GetCookies(Bff.Url("/somepath")).FirstOrDefault();
        _ = cookie.ShouldNotBeNull();
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Name.ShouldBe(Constants.Cookies.DefaultCookieName);
        cookie.Secure.ShouldBeTrue();
        cookie.Path.ShouldBe("/");
    }
}
