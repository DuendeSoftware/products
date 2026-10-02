// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests.Configuration.DependencyInjection;

public class ConfigureInternalCookieOptionsTests
{
    private static PostConfigureInternalCookieOptions CreateSut(
        string cookieAuthenticationScheme = null,
        string defaultAuthenticateScheme = null,
        string defaultScheme = null) =>
        new(
            new IdentityServerOptions
            {
                Authentication = new AuthenticationOptions
                {
                    CookieAuthenticationScheme = cookieAuthenticationScheme
                }
            },
            Options.Create(new Microsoft.AspNetCore.Authentication.AuthenticationOptions
            {
                DefaultAuthenticateScheme = defaultAuthenticateScheme,
                DefaultScheme = defaultScheme
            }),
            NullLoggerFactory.Instance);

    [Fact]
    public void decorates_built_in_default_scheme_with_identity_server_cookie_builder()
    {
        var sut = CreateSut();
        var options = new CookieAuthenticationOptions();

        sut.PostConfigure(IdentityServerConstants.DefaultCookieAuthenticationScheme, options);

        options.Cookie.ShouldBeOfType<IdentityServerCookieBuilder>();
    }

    [Fact]
    public void decorates_built_in_external_scheme_with_identity_server_cookie_builder()
    {
        var sut = CreateSut();
        var options = new CookieAuthenticationOptions();

        sut.PostConfigure(IdentityServerConstants.ExternalCookieAuthenticationScheme, options);

        options.Cookie.ShouldBeOfType<IdentityServerCookieBuilder>();
    }

    [Fact]
    public void decorating_the_same_scheme_more_than_once_remains_idempotent()
    {
        var sut = CreateSut();
        var options = new CookieAuthenticationOptions();

        sut.PostConfigure(IdentityServerConstants.DefaultCookieAuthenticationScheme, options);
        var wrappedOnce = options.Cookie;
        sut.PostConfigure(IdentityServerConstants.DefaultCookieAuthenticationScheme, options);

        options.Cookie.ShouldBeSameAs(wrappedOnce);
    }

    [Fact]
    public void decorates_explicit_cookie_authentication_scheme()
    {
        var sut = CreateSut(cookieAuthenticationScheme: "custom-idsrv-scheme");
        var options = new CookieAuthenticationOptions();

        sut.PostConfigure("custom-idsrv-scheme", options);

        options.Cookie.ShouldBeOfType<IdentityServerCookieBuilder>();
    }

    [Fact]
    public void decorates_default_authenticate_scheme_when_no_explicit_scheme_configured()
    {
        var sut = CreateSut(defaultAuthenticateScheme: "Identity.Application");
        var options = new CookieAuthenticationOptions();

        sut.PostConfigure("Identity.Application", options);

        options.Cookie.ShouldBeOfType<IdentityServerCookieBuilder>();
    }

    [Fact]
    public void decorates_default_scheme_when_no_explicit_or_authenticate_scheme_configured()
    {
        var sut = CreateSut(defaultScheme: "Identity.Application");
        var options = new CookieAuthenticationOptions();

        sut.PostConfigure("Identity.Application", options);

        options.Cookie.ShouldBeOfType<IdentityServerCookieBuilder>();
    }

    [Fact]
    public void does_not_decorate_unrelated_cookie_schemes()
    {
        var sut = CreateSut(cookieAuthenticationScheme: "custom-idsrv-scheme");
        var options = new CookieAuthenticationOptions();
        var originalBuilder = options.Cookie;

        sut.PostConfigure("some-unrelated-scheme", options);

        options.Cookie.ShouldBeSameAs(originalBuilder);
    }

    [Fact]
    public void preserves_user_configured_cookie_builder_customization()
    {
        var sut = CreateSut();
        var options = new CookieAuthenticationOptions();
        options.Cookie.Name = "user-customized-name";
        options.Cookie.Domain = "example.com";

        sut.PostConfigure(IdentityServerConstants.DefaultCookieAuthenticationScheme, options);

        options.Cookie.Name.ShouldBe("user-customized-name");
        options.Cookie.Domain.ShouldBe("example.com");
    }
}
