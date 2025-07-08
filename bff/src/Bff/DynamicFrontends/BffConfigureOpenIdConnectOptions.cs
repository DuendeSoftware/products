// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends;

internal class BffConfigureOpenIdConnectOptions(
    TimeProvider timeProvider,
    CurrentFrontendAccessor currentFrontendAccessor,
    IOptions<BffConfiguration> bffConfiguration,
    IOptions<BffOptions> bffOptions
    ) : IConfigureNamedOptions<OpenIdConnectOptions>
{
    public void Configure(OpenIdConnectOptions options) { }

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        // Normally, this is added by AuthenticationBuilder.PostConfigureAuthenticationSchemeOptions
        // but this is private API, so we need to do it ourselves.
        options.TimeProvider = timeProvider;
        var defaultOptionsValue = bffOptions.Value;
        var bffConfigurationValue = bffConfiguration.Value;

        // Apply the programmatic defaults for OpenID Connect options
        defaultOptionsValue.ConfigureOpenIdConnectDefaults?.Invoke(options);

        // Apply the defaults from the BFF configuration
        bffConfigurationValue.DefaultOidcSettings?.ApplyTo(options);

        if (defaultOptionsValue.BackchannelHttpHandler != null && options.BackchannelHttpHandler == null)
        {
            options.BackchannelHttpHandler = defaultOptionsValue.BackchannelHttpHandler;
        }

        // See if there is a frontend selected
        // If so, apply the frontend's OpenID Connect options
        if (!currentFrontendAccessor.TryGet(out var frontEnd))
        {
            return;
        }

        options.SignInScheme = frontEnd.CookieSchemeName;
        options.SignOutScheme = frontEnd.CookieSchemeName;

        frontEnd.ConfigureOpenIdConnectOptions?.Invoke(options);
    }
}
