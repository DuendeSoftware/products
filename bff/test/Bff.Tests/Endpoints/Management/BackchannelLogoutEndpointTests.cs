// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management
{
    public class BackchannelLogoutEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
    {
        [Fact]
        public async Task backchannel_logout_should_allow_anonymous()
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

            var response = await BffHost.HttpClient.PostAsync(BffHost.Url("/bff/backchannel"), null);
            response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task backchannel_logout_endpoint_should_signout()
        {
            await BffHost.BffLoginAsync("alice", "sid123");

            await IdentityServerHost.RevokeSessionCookieAsync();

            (await BffHost.GetIsUserLoggedInAsync()).ShouldBeFalse();
        }

        [Fact]
        public async Task backchannel_logout_endpoint_for_incorrect_sub_should_not_logout_user()
        {
            await BffHost.BffLoginAsync("alice", "sid123");

            await IdentityServerHost.CreateIdentityServerSessionCookieAsync("bob", "sid123");

            await IdentityServerHost.RevokeSessionCookieAsync();

            (await BffHost.GetIsUserLoggedInAsync()).ShouldBeTrue();
        }

        [Fact]
        public async Task backchannel_logout_endpoint_for_incorrect_sid_should_not_logout_user()
        {
            await BffHost.BffLoginAsync("alice", "sid123");

            await IdentityServerHost.CreateIdentityServerSessionCookieAsync("alice", "sid999");

            await IdentityServerHost.RevokeSessionCookieAsync();

            (await BffHost.GetIsUserLoggedInAsync()).ShouldBeTrue();
        }


        [Fact]
        public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_only_logout_one_session()
        {
            BffHost.BffOptions.BackchannelLogoutAllUserSessions = false;

            await BffHost.BffLoginAsync("alice", "sid1");
            BffHost.BrowserClient.RemoveCookie("bff");
            await BffHost.BffLoginAsync("alice", "sid2");

            {
                var store = BffHost.Resolve<IUserSessionStore>();
                var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" });
                sessions.Count().ShouldBe(2);
            }
            
            await IdentityServerHost.RevokeSessionCookieAsync();

            {
                var store = BffHost.Resolve<IUserSessionStore>();
                var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" });
                var session = sessions.Single();
                session.SessionId.ShouldBe("sid1");
            }
        }

        [Fact]
        public async Task when_BackchannelLogoutAllUserSessions_is_true_backchannel_logout_should_logout_all_sessions()
        {
            BffHost.BffOptions.BackchannelLogoutAllUserSessions = true;

            await BffHost.BffLoginAsync("alice", "sid1");
            BffHost.BrowserClient.RemoveCookie("bff");
            await BffHost.BffLoginAsync("alice", "sid2");

            {
                var store = BffHost.Resolve<IUserSessionStore>();
                var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" });
                sessions.Count().ShouldBe(2);
            }

            await IdentityServerHost.RevokeSessionCookieAsync();

            {
                var store = BffHost.Resolve<IUserSessionStore>();
                var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" });
                sessions.ShouldBeEmpty();
            }
        }
    }
}
