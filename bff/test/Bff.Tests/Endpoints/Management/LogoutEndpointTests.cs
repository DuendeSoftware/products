// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Tests.Endpoints.Management;

public class LogoutEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    [Fact]
    public async Task logout_endpoint_should_allow_anonymous()
    {
        BffHost.OnConfigureServices += svcs =>
        {
            svcs.AddAuthorization(opts =>
            {
                opts.FallbackPolicy =
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
        };
        await BffHost.InitializeAsync();

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task logout_endpoint_should_signout()
    {
        await BffHost.BffLoginAsync("alice", "sid123");

        await BffHost.BffLogoutAsync("sid123");

        (await BffHost.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task logout_endpoint_for_authenticated_should_require_sid()
    {
        await BffHost.BffLoginAsync("alice", "sid123");

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await BffHost.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task logout_endpoint_for_authenticated_when_require_option_is_false_should_not_require_sid()
    {
        await BffHost.BffLoginAsync("alice", "sid123");

        BffHost.BffOptions.RequireLogoutSessionId = false;

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServerHost.Url("/connect/endsession"));
    }

    [Fact]
    public async Task logout_endpoint_for_authenticated_user_without_sid_should_succeed()
    {
        // workaround for RevokeUserRefreshTokenAsync throwing when no RT in session
        BffHost.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options =>
            {
                options.RevokeRefreshTokenOnLogout = false;
            });
        };
        await BffHost.InitializeAsync();

        await BffHost.IssueSessionCookieAsync("alice");

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServerHost.Url("/connect/endsession"));
    }

    [Fact]
    public async Task logout_endpoint_for_anonymous_user_without_sid_should_succeed()
    {
        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServerHost.Url("/connect/endsession"));
    }

    [Fact]
    public async Task logout_endpoint_should_redirect_to_external_signout_and_return_to_root()
    {
        await BffHost.BffLoginAsync("alice", "sid123");

        await BffHost.BffLogoutAsync("sid123");

        BffHost.BrowserClient.CurrentUri
            .ShouldNotBeNull()
            .ToString()
            .ToLowerInvariant()
            .ShouldBe(BffHost.Url("/"));

        (await BffHost.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task can_logout_twice()
    {
        await BffHost.BffLoginAsync("alice", "sid123");

        await BffHost.BffLogoutAsync("sid123");

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout") + "?sid=123");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServerHost.Url("/connect/endsession"));


        (await BffHost.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task logout_endpoint_should_accept_returnUrl()
    {
        await BffHost.BffLoginAsync("alice", "sid123");

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout") + "?sid=sid123&returnUrl=/foo");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServerHost.Url("/connect/endsession"));

        response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location!.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // logout
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(IdentityServerHost.Url("/account/logout"));

        response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location!.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // post logout redirect uri
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(BffHost.Url("/signout-callback-oidc"));

        response = await BffHost.BrowserClient.GetAsync(response.Headers.Location!.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // root
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/foo");
    }

    [Fact]
    public async Task logout_endpoint_should_reject_non_local_returnUrl()
    {
        await BffHost.BffLoginAsync("alice", "sid123");

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout") + "?sid=sid123&returnUrl=https://foo");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
