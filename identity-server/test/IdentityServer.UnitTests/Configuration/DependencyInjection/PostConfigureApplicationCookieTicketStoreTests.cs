// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Configuration.DependencyInjection;

public class PostConfigureApplicationCookieTicketStoreTests
{

    [Fact]
    public void can_be_constructed_without_httpcontext_and_used_later_with_httpcontext()
    {
        // Register the dependencies of the usage tracker so that we can resolve it in PostConfigure
        var httpContextAccessor = new MockHttpContextAccessor(configureServices: sp =>
        {
            sp.AddSingleton(TestLogger.Create<LicenseAccessor>());
            sp.AddSingleton<LicenseAccessor>();
            sp.AddSingleton<LicenseUsageTracker>();
        });

        // The mock http context accessor has a convenient HttpContext, but
        // initially we simulate not having it by stashing it away and setting
        // the accessor's context to null.
        var savedContext = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = null;

        var sut = new PostConfigureApplicationCookieTicketStore(
            httpContextAccessor,
            new IdentityServerOptions
            {
                Authentication = new AuthenticationOptions
                {
                    // This is needed so that we operate on the correct scheme
                    CookieAuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme
                }
            },
            Options.Create<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(new()),
            TestLogger.Create<PostConfigureApplicationCookieTicketStore>()
        );

        // Now that we've constructed, we can bring back the http context and run PostConfigure
        httpContextAccessor.HttpContext = savedContext;
        var cookieOpts = new CookieAuthenticationOptions();
        sut.PostConfigure(CookieAuthenticationDefaults.AuthenticationScheme, cookieOpts);

        cookieOpts.SessionStore.ShouldBeOfType<TicketStoreShim>();
    }
}
