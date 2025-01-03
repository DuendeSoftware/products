// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Duende.Bff.Tests.Endpoints.Management
{
    public class LogoutEndpointTests : BffIntegrationTestBase
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
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task logout_endpoint_should_signout()
        {
            await BffHost.BffLoginAsync("alice", "sid123");
            
            await BffHost.BffLogoutAsync("sid123");

            (await BffHost.GetIsUserLoggedInAsync()).Should().BeFalse();
        }

        [Fact]
        public async Task logout_endpoint_for_authenticated_should_require_sid()
        {
            await BffHost.BffLoginAsync("alice", "sid123");

            Func<Task> f = () => BffHost.BffLogoutAsync();
            await f.Should().ThrowAsync<Exception>();

            (await BffHost.GetIsUserLoggedInAsync()).Should().BeTrue();
        }
        
        [Fact]
        public async Task logout_endpoint_for_authenticated_when_require_option_is_false_should_not_require_sid()
        {
            await BffHost.BffLoginAsync("alice", "sid123");
            
            BffHost.BffOptions.RequireLogoutSessionId = false;

            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // endsession
            response.Headers.Location.ToString().ToLowerInvariant().Should().StartWith(IdentityServerHost.Url("/connect/endsession"));
        }

        [Fact]
        public async Task logout_endpoint_for_authenticated_user_without_sid_should_succeed()
        {
            // workaround for RevokeUserRefreshTokenAsync throwing when no RT in session
            BffHost.OnConfigureServices += svcs => {
                svcs.Configure<BffOptions>(options => {
                    options.RevokeRefreshTokenOnLogout = false;
                });
            };
            await BffHost.InitializeAsync();

            await BffHost.IssueSessionCookieAsync("alice");

            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // endsession
            response.Headers.Location.ToString().ToLowerInvariant().Should().StartWith(IdentityServerHost.Url("/connect/endsession"));
        }

        [Fact]
        public async Task logout_endpoint_for_anonymous_user_without_sid_should_succeed()
        {
            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout"));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // endsession
            response.Headers.Location.ToString().ToLowerInvariant().Should().StartWith(IdentityServerHost.Url("/connect/endsession"));
        }

        [Fact]
        public async Task logout_endpoint_should_redirect_to_external_signout_and_return_to_root()
        {
            await BffHost.BffLoginAsync("alice", "sid123");
            
            await BffHost.BffLogoutAsync("sid123");
            
            BffHost.BrowserClient.CurrentUri.ToString().ToLowerInvariant().Should().Be(BffHost.Url("/"));
            (await BffHost.GetIsUserLoggedInAsync()).Should().BeFalse();
        }

        [Fact]
        public async Task logout_endpoint_should_accept_returnUrl()
        {
            await BffHost.BffLoginAsync("alice", "sid123");

            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout") + "?sid=sid123&returnUrl=/foo");
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // endsession
            response.Headers.Location.ToString().ToLowerInvariant().Should().StartWith(IdentityServerHost.Url("/connect/endsession"));

            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // logout
            response.Headers.Location.ToString().ToLowerInvariant().Should().StartWith(IdentityServerHost.Url("/account/logout"));

            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // post logout redirect uri
            response.Headers.Location.ToString().ToLowerInvariant().Should().StartWith(BffHost.Url("/signout-callback-oidc"));

            response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.StatusCode.Should().Be(HttpStatusCode.Redirect); // root
            response.Headers.Location.ToString().ToLowerInvariant().Should().Be("/foo");
        }

        [Fact]
        public async Task logout_endpoint_should_reject_non_local_returnUrl()
        {
            await BffHost.BffLoginAsync("alice", "sid123");

            Func<Task> f = () => BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/logout") + "?sid=sid123&returnUrl=https://foo");
            (await f.Should().ThrowAsync<Exception>()).And.Message.Should().Contain("returnUrl");
        }
    }
}
