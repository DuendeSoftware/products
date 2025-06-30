// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Xunit.Abstractions;


namespace Duende.Bff.Tests.SessionManagement;

public class CookieSlidingTests : BffTestBase
{
    private InMemoryUserSessionStore _sessionStore => (InMemoryUserSessionStore)Bff.Resolve<IUserSessionStore>();

    public CookieSlidingTests(ITestOutputHelper output) : base(output) =>
        Bff.OnConfigureBff += bff =>
            bff.AddServerSideSessions();

    private void AdvanceClock(TimeSpan by) => The.Clock.SetUtcNow(The.Clock.GetUtcNow().Add(by));

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task user_endpoint_cookie_should_slide(BffSetupType setup)
    {
        await ConfigureBff(setup, UseSlidingCookieExpiration);

        await Bff.BrowserClient.Login();

        var sessions = await _sessionStore.GetUserSessionsAsync(Some.UserSessionsFilter());
        sessions.Count().ShouldBe(1);

        var session = sessions.Single();

        var ticketStore = Bff.Resolve<IServerTicketStore>();
        var firstTicket = await ticketStore.RetrieveAsync(session.Key);
        firstTicket.ShouldNotBeNull();

        AdvanceClock(TimeSpan.FromMinutes(8));
        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();

        var secondTicket = await ticketStore.RetrieveAsync(session.Key);
        secondTicket.ShouldNotBeNull();

        (secondTicket.Properties.IssuedUtc > firstTicket.Properties.IssuedUtc).ShouldBeTrue();
        (secondTicket.Properties.ExpiresUtc > firstTicket.Properties.ExpiresUtc).ShouldBeTrue();
    }


    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task user_endpoint_when_sliding_flag_is_passed_cookie_should_not_slide(BffSetupType setup)
    {
        await ConfigureBff(setup, UseSlidingCookieExpiration);

        await Bff.BrowserClient.Login();

        var sessions = await _sessionStore.GetUserSessionsAsync(Some.UserSessionsFilter());
        sessions.Count().ShouldBe(1);

        var session = sessions.Single();

        var ticketStore = Bff.Resolve<IServerTicketStore>();
        var firstTicket = await ticketStore.RetrieveAsync(session.Key);
        firstTicket.ShouldNotBeNull();

        AdvanceClock(TimeSpan.FromMinutes(8));
        (await Bff.BrowserClient.GetIsUserLoggedInAsync("slide=false")).ShouldBeTrue();

        var secondTicket = await ticketStore.RetrieveAsync(session.Key);
        secondTicket.ShouldNotBeNull();

        (secondTicket.Properties.IssuedUtc == firstTicket.Properties.IssuedUtc).ShouldBeTrue();
        (secondTicket.Properties.ExpiresUtc == firstTicket.Properties.ExpiresUtc).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task user_endpoint_when_uservalidate_renews_cookie_should_slide(BffSetupType setup)
    {
        var shouldRenew = false;

        await ConfigureBff(setup, cookieOptions =>
        {
            UseSlidingCookieExpiration(cookieOptions);

            // Set up the OnValidatePrincipal event to control the renewal of the cookie
            // This simulates the behavior of renewing the cookie based on some condition
            cookieOptions.Events.OnValidatePrincipal = ctx =>
            {
                ctx.ShouldRenew = shouldRenew;
                return Task.CompletedTask;
            };
        });


        await Bff.BrowserClient.Login();

        var sessions = await _sessionStore.GetUserSessionsAsync(Some.UserSessionsFilter());
        sessions.Count().ShouldBe(1);

        var session = sessions.Single();

        var ticketStore = Bff.Resolve<IServerTicketStore>();
        var firstTicket = await ticketStore.RetrieveAsync(session.Key);
        firstTicket.ShouldNotBeNull();

        shouldRenew = true;
        AdvanceClock(TimeSpan.FromSeconds(1));
        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();

        var secondTicket = await ticketStore.RetrieveAsync(session.Key);
        secondTicket.ShouldNotBeNull();

        (secondTicket.Properties.IssuedUtc > firstTicket.Properties.IssuedUtc).ShouldBeTrue();
        (secondTicket.Properties.ExpiresUtc > firstTicket.Properties.ExpiresUtc).ShouldBeTrue();
    }


    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task user_endpoint_when_uservalidate_renews_and_sliding_flag_is_passed_cookie_should_not_slide(
        BffSetupType setup)
    {
        var shouldRenew = false;

        await ConfigureBff(setup, cookieOptions =>
        {
            UseSlidingCookieExpiration(cookieOptions);

            cookieOptions.Events.OnCheckSlidingExpiration = ctx =>
            {
                ctx.ShouldRenew = shouldRenew;
                return Task.CompletedTask;
            };
        });


        await Bff.BrowserClient.Login();

        var sessions = await _sessionStore.GetUserSessionsAsync(Some.UserSessionsFilter());
        sessions.Count().ShouldBe(1);

        var session = sessions.Single();

        var ticketStore = Bff.Resolve<IServerTicketStore>();
        var firstTicket = await ticketStore.RetrieveAsync(session.Key);
        firstTicket.ShouldNotBeNull();

        shouldRenew = true;
        AdvanceClock(TimeSpan.FromSeconds(1));
        (await Bff.BrowserClient.GetIsUserLoggedInAsync("slide=false")).ShouldBeTrue();

        var secondTicket = await ticketStore.RetrieveAsync(session.Key);
        secondTicket.ShouldNotBeNull();

        (secondTicket.Properties.IssuedUtc == firstTicket.Properties.IssuedUtc).ShouldBeTrue();
        (secondTicket.Properties.ExpiresUtc == firstTicket.Properties.ExpiresUtc).ShouldBeTrue();
    }

    private static void UseSlidingCookieExpiration(CookieAuthenticationOptions options)
    {
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    }
}
