// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestInfra;

public class TestInfraTests(ITestOutputHelper output) : BffTestBase(output)
{
    [Fact]
    public async Task Can_login_to_identity_server()
    {
        await InitializeAsync();
        var client = Internet.BuildHttpClient(IdentityServer.Url());

        await client.GetAsync("/account/login")
            .CheckHttpStatusCode();
    }

    [Fact]
    public async Task Can_login_to_bff_host()
    {
        Bff.OnConfigureServices += AddManualOidcFlow;

        await InitializeAsync();
        var client = Internet.BuildHttpClient(Bff.Url());
        await client.GetAsync("/bff/login")
            .CheckHttpStatusCode();
    }

    private void AddManualOidcFlow(IServiceCollection services) =>
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                // host prefixed cookie name
                options.Cookie.Name = "__Host-spa-ef";

                // strict SameSite handling
                options.Cookie.SameSite = SameSiteMode.Strict;
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = IdentityServer.Url().ToString();

                // confidential client using code flow + PKCE
                options.ClientId = DefaultOidcClient.ClientId;
                options.ClientSecret = DefaultOidcClient.ClientSecret;
                options.ResponseType = DefaultOidcClient.ResponseType;
                options.ResponseMode = DefaultOidcClient.ResponseMode;

                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;

                // request scopes + refresh tokens
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("offline_access");
                options.BackchannelHttpHandler = Internet;
            });

    [Fact]
    public async Task Can_add_api_endpoint_to_bff_host()
    {
        Bff.OnConfigureApp += app =>
        {
            app.MapGet("/api/test", async context =>
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("Hello World");
            });
        };

        await InitializeAsync();

        var client = Internet.BuildHttpClient(Bff.Url());

        await client.GetAsync("/api/test")
            .CheckHttpStatusCode();
    }

}
