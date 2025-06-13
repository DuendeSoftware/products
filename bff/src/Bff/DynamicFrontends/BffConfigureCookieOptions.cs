// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends;

internal class BffConfigureCookieOptions(
    TimeProvider timeProvider,
    IOptions<BffConfiguration> bffConfiguration,
    IOptions<BffOptions> bffOptions,
    SelectedFrontend selectedFrontend
    ) : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _bffOptions = bffOptions.Value;

    public void Configure(CookieAuthenticationOptions options) { }

    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        // Normally, this is added by AuthenticationBuilder.PostConfigureAuthenticationSchemeOptions
        // but this is private API, so we need to do it ourselves.
        options.TimeProvider = timeProvider;
        if (selectedFrontend.TryGet(out var frontEnd))
        {
            if (frontEnd.SelectionCriteria.MatchingPath != null)
            {
                options.Cookie.Name = Constants.Cookies.SecurePrefix + "_" + frontEnd.Name;
                options.Cookie.Path = frontEnd.SelectionCriteria.MatchingPath;
            }
            else
            {
                options.Cookie.Name = Constants.Cookies.HostPrefix + "_" + frontEnd.Name;
            }

            ConfigureDefaults(options);

            frontEnd.ConfigureCookieOptions?.Invoke(options);
        }
        else if (name == BffAuthenticationSchemes.BffCookie.ToString())
        {
            options.Cookie.Name = Constants.Cookies.DefaultCookieName;

            ConfigureDefaults(options);
        }
    }

    private void ConfigureDefaults(CookieAuthenticationOptions options)
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;

        _bffOptions.ConfigureCookieDefaults?.Invoke(options);

        bffConfiguration.Value.DefaultCookieSettings?.ApplyTo(options);
    }
}
