// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sustainsys.Saml2.AspNetCore2;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.SAML2;

public static class Saml2Extensions
{
    public static IFederationGatewayBuilder AddSaml2DynamicProvider(this IFederationGatewayBuilder builder)
    {
        builder.Services.Configure<IdentityServerOptions>(options =>
        {
            options.DynamicProviders
                .AddProviderType<Saml2Handler, Saml2Options, Saml2IdentityProvider>(Saml2ProviderConfigurationModel.Type);
        });

        builder.Services.AddSingleton<IConfigureOptions<Saml2Options>, Saml2ConfigureOptions>();

        // These services are normally registered when AddAuthentication().AddSaml2() is called. But when using dynamic providers
        // we don't call AddSaml() so we have to ensure the services are registered.
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Saml2Options>, PostConfigureSaml2Options>());
        builder.Services.TryAddTransient<Saml2Handler>();

        builder.Services.AddTransient<IProviderConfigurationModelFactory, Saml2ProviderConfigurationModelFactory>();
        builder.Services.AddTransient<IFederationIdentityProviderMapper, Saml2IdentityProviderMapper>();

        return builder;
    }
}
