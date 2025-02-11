// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Duende.Bff.Tests.Endpoints.Management
{
    public class LoginEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
    {
        [Fact]
        public async Task login_should_allow_anonymous()
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

            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login"));
            response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task login_endpoint_should_challenge_and_redirect_to_root()
        {
            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login"));
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

            await IdentityServerHost.IssueSessionCookieAsync("alice");
            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

            response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldBe("/");
        }
        
        [Fact]
        public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix()
        {
            BffHost.OnConfigureServices += svcs => {
                svcs.Configure<BffOptions>(options => { 
                    options.ManagementBasePath = "/custom/bff";
                });
            };
            await BffHost.InitializeAsync();
            
            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/custom/bff/login"));
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

            await IdentityServerHost.IssueSessionCookieAsync("alice");
            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

            response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldBe("/");
        }
        
        [Fact]
        public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix_trailing_slash()
        {
            BffHost.OnConfigureServices += svcs => {
                svcs.Configure<BffOptions>(options => {
                    options.ManagementBasePath = "/custom/bff/";
                });
            };
            await BffHost.InitializeAsync();
            
            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/custom/bff/login"));
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

            await IdentityServerHost.IssueSessionCookieAsync("alice");
            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

            response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldBe("/");
        }
        
        [Fact]
        public async Task login_endpoint_should_challenge_and_redirect_to_root_with_root_prefix()
        {
            BffHost.OnConfigureServices += svcs => {
                svcs.Configure<BffOptions>(options => {
                    options.ManagementBasePath = "/";
                });
            };
            await BffHost.InitializeAsync();
            
            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/login"));
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

            await IdentityServerHost.IssueSessionCookieAsync("alice");
            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

            response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldBe("/");
        }
        
        [Fact]
        public async Task login_endpoint_with_existing_session_should_challenge()
        {
            await BffHost.BffLoginAsync("alice");

            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login"));
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));
        }

        [Fact]
        public async Task login_endpoint_should_accept_returnUrl()
        {
            var response = await BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login") + "?returnUrl=/foo");
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));

            await IdentityServerHost.IssueSessionCookieAsync("alice");
            response = await IdentityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldStartWith(BffHost.Url("/signin-oidc"));

            response = await BffHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
            response.Should().Be302Redirect();
            response.Headers.Location!.ToString().ShouldBe("/foo");
        }

        [Fact]
        public async Task login_endpoint_should_not_accept_non_local_returnUrl()
        {
            Func<Task> f = () => BffHost.BrowserClient.GetAsync(BffHost.Url("/bff/login") + "?returnUrl=https://foo");
            var exception = (await f.ShouldThrowAsync<Exception>());
            exception.Message.ShouldContain("returnUrl");
        }
    }
}
