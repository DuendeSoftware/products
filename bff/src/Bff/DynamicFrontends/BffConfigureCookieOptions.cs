// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends;

internal class BffConfigureCookieOptions(
    IOptions<BffConfiguration> bffConfiguration,
    IOptions<BffOptions> bffOptions,
    SelectedFrontend selectedFrontend
    ) : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _bffOptions = bffOptions.Value;

    public void Configure(CookieAuthenticationOptions options) { }

    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        if (selectedFrontend.TryGet(out var frontEnd))
        {

            //TODO: EV: check if this is needed
            //options.ForwardChallenge = frontEnd.OidcSchemeName;

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
        else if (name == BffAuthenticationSchemes.BffDefault.ToString())
        {
            options.Cookie.Name = Constants.Cookies.DefaultCookieName;

            // Todo: EV: check if this is needed
            //options.ForwardChallenge = ;
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
