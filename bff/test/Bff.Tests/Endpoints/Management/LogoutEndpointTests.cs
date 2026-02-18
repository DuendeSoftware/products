// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.Tests.TestInfra;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
namespace Duende.Bff.Tests.Endpoints.Management;

public class LogoutEndpointTests : BffTestBase
{
    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_should_allow_anonymous(BffSetupType setup)
    {
        Bff.OnConfigureServices += svcs =>
        {
            svcs.AddAuthorization(opts =>
            {
                opts.FallbackPolicy =
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
            });
        };

        await ConfigureBff(setup);

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_should_signout(BffSetupType setup)
    {
        await ConfigureBff(setup);

        await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.Logout();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url());

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_for_authenticated_should_require_sid(BffSetupType setup)
    {
        await ConfigureBff(setup);
        await Bff.BrowserClient.Login();

        var problem = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"))
            .ShouldBeProblem();

        problem.Errors.ShouldContainKey(JwtClaimTypes.SessionId);

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_for_authenticated_when_require_option_is_false_should_not_require_sid(
        BffSetupType setup)
    {
        await ConfigureBff(setup);
        await Bff.BrowserClient.Login();

        Bff.BffOptions.RequireLogoutSessionId = false;
        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant()
            .ShouldStartWith(IdentityServer.Url("/connect/endsession").ToString());
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_for_authenticated_user_without_sid_should_succeed(BffSetupType setup)
    {
        // Workaround to place a session cookie in the BFF without a session id claim.
        Bff.OnConfigureApp += app =>
        {
            app.MapGet("/__signin", async ctx =>
            {
                var props = new AuthenticationProperties();
                await ctx.SignInAsync(
                    new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, The.Sub)], "test", "name",
                        "role")), props);

                ctx.Response.StatusCode = 204;
            });
        };

        await ConfigureBff(setup);
        await Bff.BrowserClient.GetAsync("__signin");

        // workaround for RevokeUserRefreshTokenAsync throwing when no RT in session
        Bff.BffOptions.RevokeRefreshTokenOnLogout = false;

        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant()
            .ShouldStartWith(IdentityServer.Url("/connect/endsession").ToString());
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_for_anonymous_user_without_sid_should_succeed(BffSetupType setup)
    {
        await ConfigureBff(setup);

        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant()
            .ShouldStartWith(IdentityServer.Url("/connect/endsession").ToString());
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task can_logout_twice(BffSetupType setup)
    {
        await ConfigureBff(setup);
        await Bff.BrowserClient.Login();

        var sid = await Bff.BrowserClient.GetSid();
        await Bff.BrowserClient.Logout(sid)
            .CheckHttpStatusCode();

        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;
        var response = await Bff.BrowserClient.Logout(sid);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant()
            .ShouldStartWith(IdentityServer.Url("/connect/endsession").ToString());


        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }


    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_should_accept_returnUrl(BffSetupType setup)
    {
        Bff.OnConfigureApp += app => app.MapGet("/foo", () => "foo'd you");

        await ConfigureBff(setup);
        await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.Logout(returnUrl: new Uri("/foo", UriKind.Relative))
            .CheckHttpStatusCode();

        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/foo"));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task logout_endpoint_should_reject_non_local_returnUrl(BffSetupType setup)
    {
        await ConfigureBff(setup);
        await Bff.BrowserClient.Login();

        var problem = await Bff.BrowserClient.Logout(returnUrl: new Uri("https://foo"))
            .ShouldBeProblem();

        problem.Errors.ShouldContainKey(Constants.RequestParameters.ReturnUrl);
    }
}
