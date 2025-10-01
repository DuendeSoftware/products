using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication.WsFederation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.WsFederation;

public static class WsFederationExtensions
{
    public static IFederationGatewayBuilder AddWsFederationDynamicProvider(this IFederationGatewayBuilder builder)
    {
        builder.Services.Configure<IdentityServerOptions>(options =>
        {
            options.DynamicProviders
                .AddProviderType<WsFederationHandler, WsFederationOptions, WsFederationProvider>(WsFederationProviderConfigurationModel.Type);
        });

        builder.Services.AddSingleton<IConfigureOptions<WsFederationOptions>, WsFederationProviderConfigureOptions>();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<WsFederationOptions>, WsFederationPostConfigureOptions>());
        builder.Services.TryAddTransient<WsFederationHandler>();

        builder.Services.AddTransient<IProviderConfigurationModelFactory, WsFederationProviderConfigurationModelFactory>();
        builder.Services.AddTransient<IFederationIdentityProviderMapper, WsFederationProviderMapper>();

        return builder;
    }
}
