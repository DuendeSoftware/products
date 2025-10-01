using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication.Google;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.Google;

public static class GoogleExtensions
{
    public static IFederationGatewayBuilder AddGoogleDynamicProvider(this IFederationGatewayBuilder builder)
    {
        builder.Services.Configure<IdentityServerOptions>(options =>
        {
            options.DynamicProviders
                .AddProviderType<GoogleHandler, GoogleOptions, GoogleIdentityProvider>(GoogleProviderConfigurationModel.Type);
        });

        builder.Services.ConfigureOptions<GoogleDynamicConfigureOptions>();
        builder.Services.ConfigureOptions<OAuthPostConfigureOptions<GoogleOptions, GoogleHandler>>();

        builder.Services.AddTransient<IProviderConfigurationModelFactory, GoogleProviderConfigurationModelFactory>();
        builder.Services.AddTransient<IFederationIdentityProviderMapper, GoogleIdentityProviderMapper>();

        return builder;
    }
}
