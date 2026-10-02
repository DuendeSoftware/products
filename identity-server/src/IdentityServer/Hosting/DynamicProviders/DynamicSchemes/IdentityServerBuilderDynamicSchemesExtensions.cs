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
    // Marker registered on the first in-memory identity provider registration. Its presence records
    // that the in-memory store has already been registered, so later in-memory calls do not append a
    // redundant IIdentityProviderStore descriptor. This is decorator-agnostic: it does not matter
    // whether the active store is wrapped by AddIdentityProviderStore<T>() (non-caching) or
    // AddIdentityProviderStoreCache<T>() (caching); the marker is checked rather than the wrapper type.
    private sealed class InMemoryIdentityProviderStoreMarker;

    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds the in memory identity provider store.
        /// </summary>
        /// <remarks>
        /// Providers registered by multiple calls to any in-memory identity provider
        /// registration method are accumulated. Each supplied collection is retained by
        /// reference, so callers may mutate their own collection at runtime and have the
        /// changes observed by the store.
        /// </remarks>
        /// <param name="providers">The identity providers to register.</param>
        /// <returns>The <see cref="IIdentityServerBuilder"/>.</returns>
        public IIdentityServerBuilder AddInMemoryIdentityProviders(IEnumerable<IdentityProvider> providers)
        {
            // Register the caller's collection as its own IEnumerable<IdentityProvider> singleton.
            // InMemoryIdentityProviderStore takes IEnumerable<IEnumerable<IdentityProvider>>, so every
            // call to any in-memory registration method contributes an additional collection instead of
            // shadowing the previous one.
            builder.Services.AddSingleton(providers);

            // Register the in-memory store (and disable the identity provider admin) only if a previous
            // in-memory registration has not already done so, so repeated in-memory registration calls do
            // not append redundant IIdentityProviderStore descriptors (which would shadow a configured
            // cache). A later AddIdentityProviderStore<T>() still shadows the in-memory store; a subsequent
            // in-memory call does not re-register it, matching last-registration-wins store precedence.
            var isInMemoryStoreActive = builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(InMemoryIdentityProviderStoreMarker));

            if (!isInMemoryStoreActive)
            {
                builder.AddIdentityProviderStore<InMemoryIdentityProviderStore>();
                builder.Services.AddSingleton<InMemoryIdentityProviderStoreMarker>();
                builder.Services.DisableAdmin<IIdentityProviderAdmin>("in-memory stores");
            }

            return builder;
        }
    }
}
