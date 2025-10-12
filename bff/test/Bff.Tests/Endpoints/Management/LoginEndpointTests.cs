// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Tests.Endpoints.Management;

public class LoginEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task login_should_allow_anonymous()
    {
        BffHost.OnConfigureServices += services =>
        {
            services.AddAuthorization(opts =>
            {
                opts.FallbackPolicy =
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
        };
        await BffHost.InitializeAsync();

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login"), _ct);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task login_endpoint_should_challenge_and_redirect_to_root()
    {
        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login"), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

        await IdentityServerHost.IssueSessionCookieAsync("alice");
        response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

        response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [Fact]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix()
    {
        BffHost.OnConfigureServices += services =>
        {
            services.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = "/custom/bff";
            });
        };
        await BffHost.InitializeAsync();

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/custom/bff/login"), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

        await IdentityServerHost.IssueSessionCookieAsync("alice");
        response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

        response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [Fact]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix_trailing_slash()
    {
        BffHost.OnConfigureServices += services =>
        {
            services.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = "/custom/bff/";
            });
        };
        await BffHost.InitializeAsync();

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/custom/bff/login"), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

        await IdentityServerHost.IssueSessionCookieAsync("alice");
        response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

        response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [Fact]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_root_prefix()
    {
        BffHost.OnConfigureServices += services =>
        {
            services.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = "/";
            });
        };
        await BffHost.InitializeAsync();

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/login"), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

        await IdentityServerHost.IssueSessionCookieAsync("alice");
        response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

        response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [Fact]
    public async Task login_endpoint_with_existing_session_should_challenge()
    {
        await BffHost.BffLoginAsync("alice");

        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login"), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));
    }

    [Fact]
    public async Task login_endpoint_should_accept_returnUrl()
    {
        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login") + "?returnUrl=/foo", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

        await IdentityServerHost.IssueSessionCookieAsync("alice");
        response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

        response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString(), _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/foo");
    }

    [Fact]
    public async Task login_endpoint_should_not_accept_non_local_returnUrl()
    {
        var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login") + "?returnUrl=https://foo", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
