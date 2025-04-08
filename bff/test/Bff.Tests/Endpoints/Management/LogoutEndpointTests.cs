// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestHosts;
using Duende.Bff.Tests.TestInfra;
using Duende.IdentityModel;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;

public class LogoutEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    [Fact]
    public async Task logout_endpoint_should_allow_anonymous()
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
        await Bff.InitializeAsync();

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task logout_endpoint_should_signout()
    {
        await Bff.BffLoginAsync("alice", "sid123");

        await Bff.BffLogoutAsync("sid123");

        (await Bff.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task logout_endpoint_for_authenticated_should_require_sid()
    {
        await Bff.BffLoginAsync("alice", "sid123");

        var problem = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"))
            .ShouldBeProblem();

        problem.Errors.ShouldContainKey(JwtClaimTypes.SessionId);

        (await Bff.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task logout_endpoint_for_authenticated_when_require_option_is_false_should_not_require_sid()
    {
        await Bff.BffLoginAsync("alice", "sid123");

        Bff.BffOptions.RequireLogoutSessionId = false;

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServer.Url("/connect/endsession"));
    }

    [Fact]
    public async Task logout_endpoint_for_authenticated_user_without_sid_should_succeed()
    {
        // workaround for RevokeUserRefreshTokenAsync throwing when no RT in session
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options =>
            {
                options.RevokeRefreshTokenOnLogout = false;
            });
        };
        await Bff.InitializeAsync();

        await Bff.IssueSessionCookieAsync("alice");

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServer.Url("/connect/endsession"));
    }

    [Fact]
    public async Task logout_endpoint_for_anonymous_user_without_sid_should_succeed()
    {
        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServer.Url("/connect/endsession"));
    }

    [Fact]
    public async Task logout_endpoint_should_redirect_to_external_signout_and_return_to_root()
    {
        await Bff.BffLoginAsync("alice", "sid123");

        await Bff.BffLogoutAsync("sid123");

        Bff.BrowserClient.CurrentUri
            .ShouldNotBeNull()
            .ToString()
            .ToLowerInvariant()
            .ShouldBe(Bff.Url("/"));

        (await Bff.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task can_logout_twice()
    {
        await Bff.BffLoginAsync("alice", "sid123");

        await Bff.BffLogoutAsync("sid123");

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout") + "?sid=123");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServer.Url("/connect/endsession"));


        (await Bff.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task logout_endpoint_should_accept_returnUrl()
    {
        await Bff.BffLoginAsync("alice", "sid123");

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout") + "?sid=sid123&returnUrl=/foo");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServer.Url("/connect/endsession"));

        response = await IdentityServer.BrowserClient.GetAsync(response.Headers.Location!.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // logout
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServer.Url("/account/logout"));

        response = await IdentityServer.BrowserClient.GetAsync(response.Headers.Location!.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // post logout redirect uri
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(Bff.Url("/signout-callback-oidc"));

        response = await Bff.BrowserClient.GetAsync(response.Headers.Location!.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // root
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/foo");
    }

    [Fact]
    public async Task logout_endpoint_should_reject_non_local_returnUrl()
    {
        await Bff.BffLoginAsync("alice", "sid123");

        var problem = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/logout") + "?sid=sid123&returnUrl=https://foo")
            .ShouldBeProblem();

        problem.Errors.ShouldContainKey(Constants.RequestParameters.ReturnUrl);
    }
}
