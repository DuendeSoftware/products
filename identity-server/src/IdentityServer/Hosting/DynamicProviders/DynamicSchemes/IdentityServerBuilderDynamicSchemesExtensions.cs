// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;

namespace Microsoft.Extensions.DependencyInjection;
/// <summary>
/// Add extension methods for configuring generic dynamic providers.
/// </summary>
public static class IdentityServerBuilderDynamicSchemesExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds the in memory identity provider store.
        /// </summary>
        /// <param name="providers"></param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryIdentityProviders(IEnumerable<IdentityProvider> providers)
        {
            builder.Services.AddSingleton(providers);
            builder.AddIdentityProviderStore<InMemoryIdentityProviderStore>();
            builder.Services.DisableAdmin<IIdentityProviderAdmin>("in-memory stores");

            return builder;
        }
    }
}
