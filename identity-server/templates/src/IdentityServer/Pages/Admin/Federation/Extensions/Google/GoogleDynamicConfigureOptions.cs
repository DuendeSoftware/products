using Duende.IdentityServer.Hosting.DynamicProviders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.Google;

class GoogleDynamicConfigureOptions(
    IHttpContextAccessor httpContextAccessor,
    ILogger<GoogleDynamicConfigureOptions> logger)
    : ConfigureAuthenticationOptions<GoogleOptions, GoogleIdentityProvider>(httpContextAccessor, logger)
{
    protected override void Configure(
        ConfigureAuthenticationContext<GoogleOptions, GoogleIdentityProvider> context)
    {
        var provider = context.IdentityProvider;
        var options = context.AuthenticationOptions;

        options.ClientId = provider.ClientId;
        options.ClientSecret = provider.ClientSecret;
        options.ClaimActions.MapAll();

        options.SignInScheme = context.DynamicProviderOptions.SignInScheme;
        options.CallbackPath = context.PathPrefix + "/signin";
    }
}
