// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Stores.Default;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using UnitTests.Common;

namespace UnitTests.Stores.Default;

public class ServerSideSessionCookieEventsTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    [Fact]
    public async Task OnCheckSlidingExpiration_if_force_renewal_cookie_flag_not_present_does_not_change_should_renew()
    {
        var context = CreateSlidingExpirationContext(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1));

        await ServerSideSessionCookieEvents.OnCheckSlidingExpiration(context, _timeProvider);

        context.ShouldRenew.ShouldBeFalse();
    }

    [Fact]
    public async Task OnCheckSlidingExpiration_if_force_renewal_cookie_flag_present_sets_should_renew_true()
    {
        var context = CreateSlidingExpirationContext(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5), true);

        await ServerSideSessionCookieEvents.OnCheckSlidingExpiration(context, _timeProvider);

        context.ShouldRenew.ShouldBeTrue();
    }

    [Fact]
    public async Task OnCheckSlidingExpiration_if_force_renewal_cookie_flag_present_removes_flag()
    {
        var context = CreateSlidingExpirationContext(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5), true);

        await ServerSideSessionCookieEvents.OnCheckSlidingExpiration(context, _timeProvider);

        context.Properties.Items.Keys.ShouldNotContain(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    public async Task
        OnCheckSlidingExpiration_if_force_renewal_cookie_flag_present_but_ticket_has_expired_should_not_change_renew()
    {
        var context = CreateSlidingExpirationContext(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1), true);

        await ServerSideSessionCookieEvents.OnCheckSlidingExpiration(context, _timeProvider);

        context.ShouldRenew.ShouldBeFalse();
    }

    private CookieSlidingExpirationContext CreateSlidingExpirationContext(DateTime expiresUtc, bool forceRenew = false)
    {
        var httpContext = new MockHttpContextAccessor().HttpContext;
        var authScheme = new AuthenticationScheme("Test", "Test", typeof(MockAuthenticationHandler));
        var authOptions = new CookieAuthenticationOptions();
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true
        };
        if (forceRenew)
        {
            authProperties.SetString(IdentityServerConstants.ForceCookieRenewalFlag, string.Empty);
        }

        var authTicket = new AuthenticationTicket(new ClaimsPrincipal([]), authProperties, "Test")
        {
            Properties =
            {
                ExpiresUtc = expiresUtc
            }
        };
        var context = new CookieSlidingExpirationContext(httpContext!, authScheme, authOptions, authTicket,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15))
        {
            ShouldRenew = false
        };

        return context;
    }
}
