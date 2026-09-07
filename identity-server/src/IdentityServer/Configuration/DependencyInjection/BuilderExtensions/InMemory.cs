// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder extension methods for registering in-memory services
/// </summary>
public static class IdentityServerBuilderExtensionsInMemory
{
    private const string InMemoryStoreDescription = "in-memory stores";

    /// <summary>
    /// Disables all three resource admin services. Any in-memory resource method replaces the
    /// whole <see cref="Duende.IdentityServer.Stores.IResourceStore"/>, so none of the
    /// resource admins can operate against it.
    /// </summary>
    private static void DisableResourceAdmins(this IIdentityServerBuilder builder)
    {
        builder.Services.DisableAdmin<IIdentityResourceAdmin>(InMemoryStoreDescription);
        builder.Services.DisableAdmin<IApiResourceAdmin>(InMemoryStoreDescription);
        builder.Services.DisableAdmin<IApiScopeAdmin>(InMemoryStoreDescription);
    }

    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds the in memory caching.
        /// </summary>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryCaching()
        {
            if (!builder.Services.Any(d =>
                    d.ServiceType == typeof(HybridCache) &&
                    d.IsKeyedService &&
                    ServiceProviderKeys.ConfigurationStoreCache.Equals(d.ServiceKey)))
            {
                builder.Services.AddKeyedHybridCache(ServiceProviderKeys.ConfigurationStoreCache);
            }

            if (!builder.Services.Any(d =>
                    d.ServiceType == typeof(HybridCache) &&
                    d.IsKeyedService &&
                    ServiceProviderKeys.OperationalStoreCache.Equals(d.ServiceKey)))
            {
                builder.Services.AddKeyedHybridCache(ServiceProviderKeys.OperationalStoreCache);
            }

            return builder;
        }

        /// <summary>
        /// Adds the in memory identity resources.
        /// </summary>
        /// <remarks>
        /// This replaces the entire <see cref="IResourceStore"/> with an <see cref="InMemoryResourcesStore"/> that
        /// only contains the identity resources supplied here. Calling any of the
        /// <c>AddInMemoryIdentityResources</c>, <c>AddInMemoryApiResources</c>, or <c>AddInMemoryApiScopes</c>
        /// methods replaces the whole store, not just one category. All identity resource, API resource, and API
        /// scope collections needed by the host must be registered in memory (via these methods) before the store
        /// is used, otherwise categories not registered will be unavailable.
        /// </remarks>
        /// <param name="identityResources">The identity resources.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryIdentityResources(IEnumerable<IdentityResource> identityResources)
        {
            builder.Services.AddSingleton(identityResources);
            builder.AddResourceStore<InMemoryResourcesStore>();
            builder.DisableResourceAdmins();

            return builder;
        }

        /// <summary>
        /// Adds the in memory identity resources.
        /// </summary>
        /// <param name="section">The configuration section containing the configuration data.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryIdentityResources(IConfigurationSection section)
        {
            var resources = new List<IdentityResource>();
            section.Bind(resources);

            return builder.AddInMemoryIdentityResources(resources);
        }

        /// <summary>
        /// Adds the in memory API resources.
        /// </summary>
        /// <remarks>
        /// This replaces the entire <see cref="IResourceStore"/> with an <see cref="InMemoryResourcesStore"/> that
        /// only contains the API resources supplied here. Calling any of the <c>AddInMemoryIdentityResources</c>,
        /// <c>AddInMemoryApiResources</c>, or <c>AddInMemoryApiScopes</c> methods replaces the whole store, not just
        /// one category. All identity resource, API resource, and API scope collections needed by the host must be
        /// registered in memory (via these methods) before the store is used, otherwise categories not registered
        /// will be unavailable.
        /// </remarks>
        /// <param name="apiResources">The API resources.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryApiResources(IEnumerable<ApiResource> apiResources)
        {
            builder.Services.AddSingleton(apiResources);
            builder.AddResourceStore<InMemoryResourcesStore>();
            builder.DisableResourceAdmins();

            return builder;
        }

        /// <summary>
        /// Adds the in memory API resources.
        /// </summary>
        /// <param name="section">The configuration section containing the configuration data.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryApiResources(IConfigurationSection section)
        {
            var resources = new List<ApiResource>();
            section.Bind(resources);

            return builder.AddInMemoryApiResources(resources);
        }

        /// <summary>
        /// Adds the in memory API scopes.
        /// </summary>
        /// <remarks>
        /// This replaces the entire <see cref="IResourceStore"/> with an <see cref="InMemoryResourcesStore"/> that
        /// only contains the API scopes supplied here. Calling any of the <c>AddInMemoryIdentityResources</c>,
        /// <c>AddInMemoryApiResources</c>, or <c>AddInMemoryApiScopes</c> methods replaces the whole store, not just
        /// one category. All identity resource, API resource, and API scope collections needed by the host must be
        /// registered in memory (via these methods) before the store is used, otherwise categories not registered
        /// will be unavailable.
        /// </remarks>
        /// <param name="apiScopes">The API scopes.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryApiScopes(IEnumerable<ApiScope> apiScopes)
        {
            builder.Services.AddSingleton(apiScopes);
            builder.AddResourceStore<InMemoryResourcesStore>();
            builder.DisableResourceAdmins();

            return builder;
        }

        /// <summary>
        /// Adds the in memory scopes.
        /// </summary>
        /// <param name="section">The configuration section containing the configuration data.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryApiScopes(IConfigurationSection section)
        {
            var resources = new List<ApiScope>();
            section.Bind(resources);

            return builder.AddInMemoryApiScopes(resources);
        }

        /// <summary>
        /// Adds in memory clients using an ICollection. This allows
        /// Duende.Configuration to use in memory clients for demos and testing.
        /// </summary>
        /// <param name="clients">The clients.</param>
        public IIdentityServerBuilder AddInMemoryClients(ICollection<Client> clients)
        {
            builder.Services.AddSingleton(clients);
            return builder.AddInMemoryClients((IEnumerable<Client>)clients);
        }

        /// <summary>
        /// Adds the in memory clients.
        /// </summary>
        /// <param name="clients">The clients.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryClients(IEnumerable<Client> clients)
        {
            builder.Services.AddSingleton(clients);

            builder.AddClientStore<InMemoryClientStore>();
            builder.Services.DisableAdmin<IClientAdmin>(InMemoryStoreDescription);

            // Only replace the framework-default CORS policy services (Default or Storage-backed)
            // with the in-memory implementation. A custom ICorsPolicyService registered by the host
            // must not be overwritten.
            builder.Services.TryAddTransientOrDefault<ICorsPolicyService, DefaultCorsPolicyService, InMemoryCorsPolicyService>();
            builder.Services.TryAddTransientOrDefault<ICorsPolicyService, StorageCorsPolicyService, InMemoryCorsPolicyService>();

            return builder;
        }

        /// <summary>
        /// Adds the in memory clients.
        /// </summary>
        /// <param name="section">The configuration section containing the configuration data.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryClients(IConfigurationSection section)
        {
            var clients = new List<Client>();
            section.Bind(clients);

            return builder.AddInMemoryClients(clients);
        }

        /// <summary>
        /// Adds the in memory stores.
        /// </summary>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryPersistedGrants()
        {
            builder.Services.AddSingleton<IPersistedGrantStore, InMemoryPersistedGrantStore>();
            builder.Services.AddSingleton<IDeviceFlowStore, InMemoryDeviceFlowStore>();

            return builder;
        }

        /// <summary>
        /// Adds the in memory pushed authorization request store.
        /// </summary>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemoryPushedAuthorizationRequests()
        {
            builder.Services.RemoveAll<IPushedAuthorizationRequestStore>();
            builder.Services.AddSingleton<IPushedAuthorizationRequestStore, InMemoryPushedAuthorizationRequestStore>();
            return builder;
        }

        /// <summary>
        /// Adds the in-memory SAML service provider store.
        /// </summary>
        /// <param name="serviceProviders">The SAML service providers.</param>
        /// <returns></returns>
        public IIdentityServerBuilder AddInMemorySamlServiceProviders(IEnumerable<SamlServiceProvider> serviceProviders)
        {
            builder.Services.AddSingleton(serviceProviders);
            builder.AddSamlServiceProviderStore<InMemorySamlServiceProviderStore>();
            builder.Services.DisableAdmin<ISamlServiceProviderAdmin>(InMemoryStoreDescription);
            return builder;
        }
    }
}
