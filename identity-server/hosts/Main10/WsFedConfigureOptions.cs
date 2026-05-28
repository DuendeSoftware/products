// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Hosting.DynamicProviders;
using Microsoft.AspNetCore.Authentication.WsFederation;

namespace IdentityServerHost;

/// <summary>
/// Configures <see cref="WsFederationOptions"/> for dynamic WS-Federation providers
/// by reading settings from the <see cref="WsFedProvider"/> model stored in the identity
/// provider store.
/// </summary>
internal class WsFedConfigureOptions(
    IHttpContextAccessor httpContextAccessor,
    ILogger<WsFedConfigureOptions> logger)
    : ConfigureAuthenticationOptions<WsFederationOptions, WsFedProvider>(httpContextAccessor, logger)
{
    protected override void Configure(ConfigureAuthenticationContext<WsFederationOptions, WsFedProvider> context)
    {
        context.AuthenticationOptions.MetadataAddress = context.IdentityProvider.MetadataAddress!;
        context.AuthenticationOptions.Wtrealm = context.IdentityProvider.Wtrealm!;
        context.AuthenticationOptions.SignInScheme = context.DynamicProviderOptions.SignInScheme;
        context.AuthenticationOptions.SignOutScheme = context.DynamicProviderOptions.SignOutScheme;
        context.AuthenticationOptions.CallbackPath = context.PathPrefix + "/signin";
        context.AuthenticationOptions.RemoteSignOutPath = context.PathPrefix + "/signout";
    }
}
