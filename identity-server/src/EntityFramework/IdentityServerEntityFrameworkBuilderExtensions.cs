// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Services;
using Duende.IdentityServer.EntityFramework.Storage;
using Duende.IdentityServer.EntityFramework.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to add EF database support to IdentityServer.
/// </summary>
public static class IdentityServerEntityFrameworkBuilderExtensions
{
    /// <summary>
    /// Configures EF implementation of IClientStore, IResourceStore, and ICorsPolicyService with IdentityServer.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="storeOptionsAction">The store options action.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddConfigurationStore(
        this IIdentityServerBuilder builder,
        Action<ConfigurationStoreOptions>? storeOptionsAction = null) => builder.AddConfigurationStore<ConfigurationDbContext>(storeOptionsAction);

    /// <summary>
    /// Configures EF implementation of IClientStore, IResourceStore, and ICorsPolicyService with IdentityServer.
    /// </summary>
    /// <typeparam name="TContext">The IConfigurationDbContext to use.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="storeOptionsAction">The store options action.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddConfigurationStore<TContext>(
        this IIdentityServerBuilder builder,
        Action<ConfigurationStoreOptions>? storeOptionsAction = null)
        where TContext : DbContext, IConfigurationDbContext
    {
        _ = builder.Services.AddConfigurationDbContext<TContext>(storeOptionsAction);

        _ = builder.AddClientStore<ClientStore>();
        _ = builder.AddResourceStore<ResourceStore>();
        _ = builder.AddCorsPolicyService<CorsPolicyService>();
        _ = builder.AddIdentityProviderStore<IdentityProviderStore>();

        return builder;
    }

    /// <summary>
    /// Configures caching for IClientStore, IResourceStore, and ICorsPolicyService with IdentityServer.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddConfigurationStoreCache(
        this IIdentityServerBuilder builder)
    {
        _ = builder.AddInMemoryCaching();

        // add the caching decorators
        _ = builder.AddClientStoreCache<ClientStore>();
        _ = builder.AddResourceStoreCache<ResourceStore>();
        _ = builder.AddCorsPolicyCache<CorsPolicyService>();
        _ = builder.AddIdentityProviderStoreCache<IdentityProviderStore>();

        return builder;
    }

    /// <summary>
    /// Configures EF implementation of IPersistedGrantStore with IdentityServer.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="storeOptionsAction">The store options action.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddOperationalStore(
        this IIdentityServerBuilder builder,
        Action<OperationalStoreOptions>? storeOptionsAction = null) => builder.AddOperationalStore<PersistedGrantDbContext>(storeOptionsAction);

    /// <summary>
    /// Configures EF implementation of IPersistedGrantStore with IdentityServer.
    /// </summary>
    /// <typeparam name="TContext">The IPersistedGrantDbContext to use.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="storeOptionsAction">The store options action.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddOperationalStore<TContext>(
        this IIdentityServerBuilder builder,
        Action<OperationalStoreOptions>? storeOptionsAction = null)
        where TContext : DbContext, IPersistedGrantDbContext
    {
        _ = builder.Services.AddOperationalDbContext<TContext>(storeOptionsAction);

        _ = builder.AddSigningKeyStore<SigningKeyStore>();
        _ = builder.AddPersistedGrantStore<PersistedGrantStore>();
        _ = builder.AddDeviceFlowStore<DeviceFlowStore>();
        _ = builder.AddServerSideSessionStore<ServerSideSessionStore>();
        _ = builder.AddPushedAuthorizationRequestStore<PushedAuthorizationRequestStore>();

        _ = builder.Services.AddSingleton<IHostedService, TokenCleanupHost>();

        return builder;
    }

    /// <summary>
    /// Adds an implementation of the IOperationalStoreNotification to IdentityServer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddOperationalStoreNotification<T>(
        this IIdentityServerBuilder builder)
        where T : class, IOperationalStoreNotification
    {
        _ = builder.Services.AddOperationalStoreNotification<T>();
        return builder;
    }
}
