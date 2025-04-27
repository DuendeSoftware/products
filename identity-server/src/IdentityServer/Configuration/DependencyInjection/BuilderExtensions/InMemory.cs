// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder extension methods for registering in-memory services
/// </summary>
public static class IdentityServerBuilderExtensionsInMemory
{
    /// <summary>
    /// Adds the in memory caching.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IIdentityServerBuilder AddInMemoryCaching(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddSingleton<IMemoryCache, MemoryCache>();
        builder.Services.TryAddTransient(typeof(ICache<>), typeof(DefaultCache<>));

        return builder;
    }

    /// <summary>
    /// Adds the in memory identity resources.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="identityResources">The identity resources.</param>
    public static IIdentityServerBuilder AddInMemoryIdentityResources(this IIdentityServerBuilder builder, IEnumerable<IdentityResource> identityResources)
    {
        builder.Services.AddSingleton(identityResources);
        builder.AddResourceStore<InMemoryResourcesStore>();

        return builder;
    }

    /// <summary>
    /// Adds the in memory identity resources.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="section">The configuration section containing the configuration data.</param>
    public static IIdentityServerBuilder AddInMemoryIdentityResources(this IIdentityServerBuilder builder, IConfigurationSection section)
    {
        var resources = new List<IdentityResource>();
        section.Bind(resources);

        return builder.AddInMemoryIdentityResources(resources);
    }

    /// <summary>
    /// Adds the in memory API resources.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="apiResources">The API resources.</param>
    public static IIdentityServerBuilder AddInMemoryApiResources(this IIdentityServerBuilder builder, IEnumerable<ApiResource> apiResources)
    {
        builder.Services.AddSingleton(apiResources);
        builder.AddResourceStore<InMemoryResourcesStore>();

        return builder;
    }

    /// <summary>
    /// Adds the in memory API resources.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="section">The configuration section containing the configuration data.</param>
    public static IIdentityServerBuilder AddInMemoryApiResources(this IIdentityServerBuilder builder, IConfigurationSection section)
    {
        var resources = new List<ApiResource>();
        section.Bind(resources);

        return builder.AddInMemoryApiResources(resources);
    }

    /// <summary>
    /// Adds the in memory API scopes.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="apiScopes">The API scopes.</param>
    public static IIdentityServerBuilder AddInMemoryApiScopes(this IIdentityServerBuilder builder, IEnumerable<ApiScope> apiScopes)
    {
        builder.Services.AddSingleton(apiScopes);
        builder.AddResourceStore<InMemoryResourcesStore>();

        return builder;
    }

    /// <summary>
    /// Adds the in memory scopes.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="section">The configuration section containing the configuration data.</param>
    public static IIdentityServerBuilder AddInMemoryApiScopes(this IIdentityServerBuilder builder, IConfigurationSection section)
    {
        var resources = new List<ApiScope>();
        section.Bind(resources);

        return builder.AddInMemoryApiScopes(resources);
    }

    /// <summary>
    /// Adds in memory clients using an ICollection. This allows
    /// Duende.Configuration to use in memory clients for demos and testing.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="clients">The clients.</param>
    public static IIdentityServerBuilder AddInMemoryClients(this IIdentityServerBuilder builder, ICollection<Client> clients)
    {
        builder.Services.AddSingleton(clients);
        return AddInMemoryClients(builder, (IEnumerable<Client>)clients);
    }

    /// <summary>
    /// Adds the in memory clients.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="clients">The clients.</param>
    public static IIdentityServerBuilder AddInMemoryClients(this IIdentityServerBuilder builder, IEnumerable<Client> clients)
    {
        builder.Services.AddSingleton(clients);

        builder.AddClientStore<InMemoryClientStore>();

        var existingCors = builder.Services.LastOrDefault(x => x.ServiceType == typeof(ICorsPolicyService));
        if (existingCors != null &&
            existingCors.ImplementationType == typeof(DefaultCorsPolicyService) &&
            existingCors.Lifetime == ServiceLifetime.Transient)
        {
            // if our default is registered, then overwrite with the InMemoryCorsPolicyService
            // otherwise don't overwrite with the InMemoryCorsPolicyService, which uses the custom one registered by the host
            builder.Services.AddTransient<ICorsPolicyService, InMemoryCorsPolicyService>();
        }

        return builder;
    }

    /// <summary>
    /// Adds the in memory clients.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="section">The configuration section containing the configuration data.</param>
    public static IIdentityServerBuilder AddInMemoryClients(this IIdentityServerBuilder builder, IConfigurationSection section)
    {
        var clients = new List<Client>();
        section.Bind(clients);

        return builder.AddInMemoryClients(clients);
    }


    /// <summary>
    /// Adds the in memory stores.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IIdentityServerBuilder AddInMemoryPersistedGrants(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddSingleton<IPersistedGrantStore, InMemoryPersistedGrantStore>();
        builder.Services.TryAddSingleton<IDeviceFlowStore, InMemoryDeviceFlowStore>();

        return builder;
    }

    /// <summary>
    /// Adds the in memory pushed authorization request store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IIdentityServerBuilder AddInMemoryPushedAuthorizationRequests(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddSingleton<IPushedAuthorizationRequestStore, InMemoryPushedAuthorizationRequestStore>();
        return builder;
    }
}
