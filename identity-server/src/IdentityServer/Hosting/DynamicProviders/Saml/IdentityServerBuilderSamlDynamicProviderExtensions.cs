// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Add extension methods for configuring SAML dynamic providers.
/// </summary>
public static class IdentityServerBuilderSamlDynamicProviderExtensions
{
    /// <summary>
    /// Adds the SAML 2.0 dynamic provider feature.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSamlDynamicProvider(this IIdentityServerBuilder builder)
    {
        builder.AddDynamicProvider<Saml2Handler, Saml2Options, SamlProvider, SamlConfigureOptions>("saml");

        // Register the public options pipeline so customers can use
        // ConfigureAuthenticationOptions<SamlAuthenticationOptions, SamlProvider>
        builder.Services.ConfigureOptions<SamlAuthenticationConfigureOptions>();

        // Register post-configure to set defaults (logger, cookie manager)
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<Saml2Options>, PostConfigureSaml2OptionsForDynamic>());

        return builder;
    }

    /// <summary>
    /// Adds the in-memory SAML provider store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="providers">The SAML providers to register.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddInMemorySamlProviders(
        this IIdentityServerBuilder builder, IEnumerable<SamlProvider> providers) =>
        builder.AddInMemoryIdentityProviders(providers.Cast<IdentityProvider>().ToList());
}
