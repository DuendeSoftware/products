// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends;

internal class BffConfigureOpenIdConnectOptions(
    SelectedFrontend selectedFrontend,
    IOptions<BffConfiguration> bffConfiguration,
    IOptions<BffOptions> bffOptions
    ) : IConfigureNamedOptions<OpenIdConnectOptions>
{
    public void Configure(OpenIdConnectOptions options) { }

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        var defaultOptionsValue = bffOptions.Value;
        var bffConfigurationValue = bffConfiguration.Value;

        var defaultCallbackPath = options.CallbackPath;
        defaultOptionsValue.ConfigureOpenIdConnectDefaults?.Invoke(options);
        bffConfigurationValue.DefaultOidcSettings?.ApplyTo(options);


        if (defaultOptionsValue.BackchannelMessageHandler != null)
        {
            options.BackchannelHttpHandler = defaultOptionsValue.BackchannelMessageHandler;
        }

        if (!selectedFrontend.TryGet(out var frontEnd))
        {
            return;
        }

        // Make sure we have a default for the callback path. 
        if (options.CallbackPath == defaultCallbackPath)
        {
            options.CallbackPath = Constants.ManagementEndpoints.SigninUrl;
        }

        options.SignInScheme = frontEnd.CookieSchemeName;
        options.SignOutScheme = frontEnd.CookieSchemeName;

        frontEnd.ConfigureOpenIdConnectOptions?.Invoke(options);
    }
}
