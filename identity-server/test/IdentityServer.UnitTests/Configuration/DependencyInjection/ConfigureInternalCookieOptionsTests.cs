// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Configuration.DependencyInjection;

public class ConfigureInternalCookieOptionsTests
{
    private static CookieAuthenticationOptions ConfigureMainCookie(IdentityServerOptions idsrvOptions)
    {
        var sut = new ConfigureInternalCookieOptions(idsrvOptions);
        var cookieOptions = new CookieAuthenticationOptions();
        sut.Configure(IdentityServerConstants.DefaultCookieAuthenticationScheme, cookieOptions);
        return cookieOptions;
    }

    private static CookieAuthenticationOptions ConfigureExternalCookie(IdentityServerOptions idsrvOptions)
    {
        var sut = new ConfigureInternalCookieOptions(idsrvOptions);
        var cookieOptions = new CookieAuthenticationOptions();
        sut.Configure(IdentityServerConstants.ExternalCookieAuthenticationScheme, cookieOptions);
        return cookieOptions;
    }

    // --- Default cookie names ---

    [Fact]
    public void main_cookie_name_defaults_to_host_prefixed_idsrv()
    {
        var options = ConfigureMainCookie(new IdentityServerOptions());
        options.Cookie.Name.ShouldBe("__Host-idsrv");
    }

    [Fact]
    public void external_cookie_name_defaults_to_host_prefixed_idsrv_external()
    {
        var options = ConfigureExternalCookie(new IdentityServerOptions());
        options.Cookie.Name.ShouldBe("__Host-idsrv.external");
    }

    // --- Custom cookie names are applied ---

    [Fact]
    public void custom_main_cookie_name_is_applied()
    {
        var idsrvOptions = new IdentityServerOptions();
        idsrvOptions.Authentication.CookieName = "my-custom-cookie";

        var options = ConfigureMainCookie(idsrvOptions);

        options.Cookie.Name.ShouldBe("my-custom-cookie");
    }

    [Fact]
    public void custom_external_cookie_name_is_applied()
    {
        var idsrvOptions = new IdentityServerOptions();
        idsrvOptions.Authentication.ExternalCookieName = "my-custom-external-cookie";

        var options = ConfigureExternalCookie(idsrvOptions);

        options.Cookie.Name.ShouldBe("my-custom-external-cookie");
    }

    [Fact]
    public void legacy_main_cookie_name_can_be_restored()
    {
        var idsrvOptions = new IdentityServerOptions();
        idsrvOptions.Authentication.CookieName = "idsrv";

        var options = ConfigureMainCookie(idsrvOptions);

        options.Cookie.Name.ShouldBe("idsrv");
    }

    [Fact]
    public void legacy_external_cookie_name_can_be_restored()
    {
        var idsrvOptions = new IdentityServerOptions();
        idsrvOptions.Authentication.ExternalCookieName = "idsrv.external";

        var options = ConfigureExternalCookie(idsrvOptions);

        options.Cookie.Name.ShouldBe("idsrv.external");
    }

    // --- __Host- prefix enforces SecurePolicy, Path, and no Domain ---

    [Fact]
    public void host_prefixed_main_cookie_sets_secure_policy_to_always()
    {
        var options = ConfigureMainCookie(new IdentityServerOptions());
        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
    }

    [Fact]
    public void host_prefixed_main_cookie_sets_path_to_root()
    {
        var options = ConfigureMainCookie(new IdentityServerOptions());
        options.Cookie.Path.ShouldBe("/");
    }

    [Fact]
    public void host_prefixed_main_cookie_clears_domain()
    {
        var options = ConfigureMainCookie(new IdentityServerOptions());
        options.Cookie.Domain.ShouldBeNull();
    }

    [Fact]
    public void host_prefixed_external_cookie_sets_secure_policy_to_always()
    {
        var options = ConfigureExternalCookie(new IdentityServerOptions());
        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
    }

    [Fact]
    public void host_prefixed_external_cookie_sets_path_to_root()
    {
        var options = ConfigureExternalCookie(new IdentityServerOptions());
        options.Cookie.Path.ShouldBe("/");
    }

    [Fact]
    public void host_prefixed_external_cookie_clears_domain()
    {
        var options = ConfigureExternalCookie(new IdentityServerOptions());
        options.Cookie.Domain.ShouldBeNull();
    }

    // --- Non-__Host- names do NOT override SecurePolicy/Path/Domain ---

    [Fact]
    public void non_host_prefixed_main_cookie_does_not_force_secure_policy()
    {
        var idsrvOptions = new IdentityServerOptions();
        idsrvOptions.Authentication.CookieName = "idsrv";

        var options = ConfigureMainCookie(idsrvOptions);

        // Default CookieSecurePolicy is SameAsRequest, not Always
        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
    }

    [Fact]
    public void non_host_prefixed_external_cookie_does_not_force_secure_policy()
    {
        var idsrvOptions = new IdentityServerOptions();
        idsrvOptions.Authentication.ExternalCookieName = "idsrv.external";

        var options = ConfigureExternalCookie(idsrvOptions);

        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
    }
}
