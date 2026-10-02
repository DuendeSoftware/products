// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Add extension methods for configuring SAML dynamic providers.
/// </summary>
public static class IdentityServerBuilderSamlDynamicProviderExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds the SAML 2.0 dynamic provider feature.
        /// </summary>
        /// <returns></returns>
        public IIdentityServerBuilder AddSamlDynamicProvider()
        {
            builder.AddDynamicProvider<Saml2Handler, Saml2Options, SamlProvider, SamlConfigureOptions>("saml");

            // Register the public options pipeline so customers can use
            // ConfigureAuthenticationOptions<SamlAuthenticationOptions, SamlProvider>
            builder.Services.ConfigureOptions<SamlAuthenticationConfigureOptions>();

            // Register post-configure to set defaults (logger, cookie manager)
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IPostConfigureOptions<Saml2Options>, PostConfigureSaml2OptionsForDynamic>());

            builder.Services.TryAddSchema(BuiltInSchemas.SamlProvider);

            return builder;
        }

        /// <summary>
        /// Adds the in-memory SAML provider store.
        /// </summary>
        /// <remarks>
        /// Providers registered by multiple calls to any in-memory identity provider
        /// registration method are accumulated rather than replacing one another. The supplied
        /// collection is retained by reference, so callers may mutate their own collection at
        /// runtime and have the changes observed by the store.
        /// </remarks>
        /// <param name="providers">The SAML providers to register.</param>
        /// <returns>The <see cref="IIdentityServerBuilder"/>.</returns>
        public IIdentityServerBuilder AddInMemorySamlProviders(IEnumerable<SamlProvider> providers) =>
            builder.AddInMemoryIdentityProviders(providers);
    }
}
