// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;


// Todo: EV: these tests are suspect. I'm not 100% sure they are correct.


public class BackchannelLogoutEndpointTests : BffTestBase
{
    public BackchannelLogoutEndpointTests(ITestOutputHelper output) : base(output)
    {
        var client = IdentityServer.AddClient(The.ClientId, Bff.Url());
        client.BackChannelLogoutUri = Bff.Url("/bff/backchannel").ToString();
        client.BackChannelLogoutSessionRequired = true;

        Bff.SetBffOptions += options =>
        {
            options.ConfigureOpenIdConnectDefaults = opt =>
            {
                The.DefaultOpenIdConnectConfiguration(opt);
            };
        };

        Bff.OnConfigureBff += bff =>
        {
            bff.AddServerSideSessions();
        };
    }


    [Fact]
    public async Task backchannel_logout_should_allow_anonymous()
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
        await InitializeAsync();

        // if you call the endpoint without a token, it should return 400
        await Bff.BrowserClient.PostAsync(Bff.Url("/bff/backchannel"), null)
            .CheckHttpStatusCode(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task backchannel_logout_endpoint_should_signout()
    {
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task backchannel_logout_endpoint_for_incorrect_sub_should_not_logout_user()
    {
        await InitializeAsync();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, "different_sub", The.Sid);

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task backchannel_logout_endpoint_for_incorrect_sid_should_not_logout_user()
    {
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different_sid");

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }


    [Fact]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_only_logout_one_session()
    {
        Bff.SetBffOptions += options =>
        {
            options.BackchannelLogoutAllUserSessions = false;
        };

        await InitializeAsync();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        // Set a Set-Cookie header to clear the "__Host-bff-auth" cookie
        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");

        await Bff.BrowserClient.Login();

        {
            var store = Bff.Resolve<IUserSessionStore>();
            var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub });
            sessions.Count().ShouldBe(2);
        }

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        {
            var store = Bff.Resolve<IUserSessionStore>();
            var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub });
            var session = sessions.Single();
            session.SessionId.ShouldBe(The.Sid);
        }
    }

    [Fact]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_logout_all_sessions()
    {
        Bff.SetBffOptions += options =>
        {
            options.BackchannelLogoutAllUserSessions = true;
        };

        await InitializeAsync();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        // Set a Set-Cookie header to clear the "__Host-bff-auth" cookie
        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");

        await Bff.BrowserClient.Login();

        {
            var store = Bff.Resolve<IUserSessionStore>();
            var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub });
            sessions.Count().ShouldBe(2);
        }

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        {
            var store = Bff.Resolve<IUserSessionStore>();
            var sessions = await store.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub });
            sessions.ShouldBeEmpty();
        }
    }
}

