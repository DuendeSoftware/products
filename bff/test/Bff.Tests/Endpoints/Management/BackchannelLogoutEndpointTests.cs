// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
using Duende.IdentityServer.Models;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;

public class BackchannelLogoutEndpointTests : BffTestBase
{
    public BackchannelLogoutEndpointTests(ITestOutputHelper output) : base(output)
    {
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

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        foreach (var client in IdentityServer.Clients)
        {
            client.BackChannelLogoutUri = Bff.Url("/bff/backchannel").ToString();
            client.BackChannelLogoutSessionRequired = true;
        }
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_should_allow_anonymous(BffSetupType setup)
    {
        ConfigureBff(setup);

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

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_should_signout(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_for_incorrect_sub_should_not_logout_user(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, "different_sub", The.Sid);

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_for_incorrect_sid_should_not_logout_user(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different_sid");

        await Bff.BrowserClient.RevokeIdentityServerSession(IdentityServer.Url());

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }


    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_only_logout_one_session(BffSetupType setup)
    {
        ConfigureBff(setup);
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

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_logout_all_sessions(BffSetupType setup)
    {
        ConfigureBff(setup);
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

