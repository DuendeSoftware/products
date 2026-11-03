// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;

namespace Duende.Bff.Tests.Endpoints;

public class NonBffSchemeAuthenticationTests : BffTestBase
{
    private const string BearerScheme = "Bearer";

    private void AddBearerSchemeAndProtectedEndpoint()
    {
        Bff.OnConfigureServices += services =>
        {
            _ = services.AddAuthentication()
                .AddJwtBearer(BearerScheme, options =>
                {
                    options.Authority = IdentityServer.Url().ToString();
                    options.Audience = IdentityServer.Url("/resources").ToString();
                    options.MapInboundClaims = false;
                    options.BackchannelHttpHandler = IdentityServer.Server.CreateHandler();
                });
        };

        Bff.OnConfigureApp += app =>
        {
            _ = app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c))
                .RequireAuthorization(policy =>
                {
                    _ = policy.AddAuthenticationSchemes(BearerScheme);
                    _ = policy.RequireAuthenticatedUser();
                });
        };
    }

    [Fact]
    public async Task calling_bearer_protected_endpoint_with_only_a_session_cookie_should_return_401()
    {
        AddBearerSchemeAndProtectedEndpoint();

        await ConfigureBff(BffSetupType.BffWithFrontend);

        _ = await Bff.BrowserClient.Login();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        req.Headers.Add("x-csrf", "1");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task calling_bearer_protected_endpoint_with_only_a_session_cookie_should_return_401_when_bearer_is_the_configured_default_scheme()
    {
        AddBearerSchemeAndProtectedEndpoint();

        Bff.OnConfigureServices += services =>
        {
            _ = services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = BearerScheme;
                options.DefaultChallengeScheme = BffAuthenticationSchemes.BffOpenIdConnect;
                options.DefaultSignOutScheme = BffAuthenticationSchemes.BffOpenIdConnect;
            });
        };

        await ConfigureBff(BffSetupType.BffWithFrontend);

        _ = await Bff.BrowserClient.Login();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        req.Headers.Add("x-csrf", "1");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task calling_user_endpoint_with_session_cookie_should_return_200_when_bearer_is_the_configured_default_scheme()
    {
        AddBearerSchemeAndProtectedEndpoint();

        Bff.OnConfigureServices += services =>
        {
            _ = services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = BearerScheme;
                options.DefaultChallengeScheme = BffAuthenticationSchemes.BffOpenIdConnect;
                options.DefaultSignOutScheme = BffAuthenticationSchemes.BffOpenIdConnect;
            });
        };

        await ConfigureBff(BffSetupType.BffWithFrontend);

        _ = await Bff.BrowserClient.Login();

        var claims = await Bff.BrowserClient.CallUserEndpointAsync();

        claims.ShouldNotBeEmpty();
        claims.Any(c => c.Type == "sub").ShouldBeTrue();
    }

    [Fact]
    public async Task challenge_on_bff_api_endpoint_for_unauthenticated_user_should_return_401()
    {
        Bff.OnConfigureApp += app =>
        {
            _ = app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c))
                .AsBffApiEndpoint()
                .RequireAuthorization();
        };

        await ConfigureBff(BffSetupType.BffWithFrontend);

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        req.Headers.Add("x-csrf", "1");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task forbid_on_non_bff_api_endpoint_for_logged_in_user_should_not_recurse()
    {
        Bff.OnConfigureApp += app =>
        {
            _ = app.Map(The.Path, c => c.ForbidAsync());
        };

        await ConfigureBff(BffSetupType.BffWithFrontend);

        _ = await Bff.BrowserClient.Login();

        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        req.Headers.Add("x-csrf", "1");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task logout_should_clear_the_frontend_session()
    {
        await ConfigureBff(BffSetupType.BffWithFrontend);

        _ = await Bff.BrowserClient.Login();
        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();

        _ = await Bff.BrowserClient.Logout();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }
}
