// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Add extension methods for configuring OIDC dynamic providers.
/// </summary>
public static class IdentityServerBuilderOidcExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds the OIDC dynamic provider feature.
        /// </summary>
        /// <returns></returns>
        public IIdentityServerBuilder AddOidcDynamicProvider()
        {
            builder.AddDynamicProvider<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider, OidcConfigureOptions>("oidc");

            // these are services from ASP.NET Core and are added manually since we're not using the
            // AddOpenIdConnect helper that we'd normally use statically on the AddAuthentication.
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>());

            builder.Services.TryAddSchema(BuiltInSchemas.OidcProvider);

            return builder;
        }

        /// <summary>
        /// Adds the in memory OIDC provider store.
        /// </summary>
        /// <remarks>
        /// Providers registered by multiple calls to any in-memory identity provider
        /// registration method are accumulated rather than replacing one another. The supplied
        /// collection is retained by reference, so callers may mutate their own collection at
        /// runtime and have the changes observed by the store.
        /// </remarks>
        /// <param name="providers">The OIDC providers to register.</param>
        /// <returns>The <see cref="IIdentityServerBuilder"/>.</returns>
        public IIdentityServerBuilder AddInMemoryOidcProviders(IEnumerable<OidcProvider> providers) => builder.AddInMemoryIdentityProviders(providers);
    }
}
