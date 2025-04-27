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
    public static IIdentityServerBuilder AddConfigurationStore(
        this IIdentityServerBuilder builder,
        Action<ConfigurationStoreOptions>? storeOptionsAction = null) => builder.AddConfigurationStore<ConfigurationDbContext>(storeOptionsAction);

    /// <summary>
    /// Configures EF implementation of IClientStore, IResourceStore, and ICorsPolicyService with IdentityServer.
    /// </summary>
    /// <typeparam name="TContext">The IConfigurationDbContext to use.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="storeOptionsAction">The store options action.</param>
    public static IIdentityServerBuilder AddConfigurationStore<TContext>(
        this IIdentityServerBuilder builder,
        Action<ConfigurationStoreOptions>? storeOptionsAction = null)
        where TContext : DbContext, IConfigurationDbContext
    {
        builder.Services.AddConfigurationDbContext<TContext>(storeOptionsAction);

        builder.AddClientStore<ClientStore>();
        builder.AddResourceStore<ResourceStore>();
        builder.AddCorsPolicyService<CorsPolicyService>();
        builder.AddIdentityProviderStore<IdentityProviderStore>();

        return builder;
    }

    /// <summary>
    /// Configures caching for IClientStore, IResourceStore, and ICorsPolicyService with IdentityServer.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IIdentityServerBuilder AddConfigurationStoreCache(
        this IIdentityServerBuilder builder)
    {
        builder.AddInMemoryCaching();

        // add the caching decorators
        builder.AddClientStoreCache<ClientStore>();
        builder.AddResourceStoreCache<ResourceStore>();
        builder.AddCorsPolicyCache<CorsPolicyService>();
        builder.AddIdentityProviderStoreCache<IdentityProviderStore>();

        return builder;
    }

    /// <summary>
    /// Configures EF implementation of IPersistedGrantStore with IdentityServer.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="storeOptionsAction">The store options action.</param>
    public static IIdentityServerBuilder AddOperationalStore(
        this IIdentityServerBuilder builder,
        Action<OperationalStoreOptions>? storeOptionsAction = null) => builder.AddOperationalStore<PersistedGrantDbContext>(storeOptionsAction);

    /// <summary>
    /// Configures EF implementation of IPersistedGrantStore with IdentityServer.
    /// </summary>
    /// <typeparam name="TContext">The IPersistedGrantDbContext to use.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="storeOptionsAction">The store options action.</param>
    public static IIdentityServerBuilder AddOperationalStore<TContext>(
        this IIdentityServerBuilder builder,
        Action<OperationalStoreOptions>? storeOptionsAction = null)
        where TContext : DbContext, IPersistedGrantDbContext
    {
        builder.Services.AddOperationalDbContext<TContext>(storeOptionsAction);

        builder.AddSigningKeyStore<SigningKeyStore>();
        builder.AddPersistedGrantStore<PersistedGrantStore>();
        builder.AddDeviceFlowStore<DeviceFlowStore>();
        builder.AddServerSideSessionStore<ServerSideSessionStore>();
        builder.AddPushedAuthorizationRequestStore<PushedAuthorizationRequestStore>();

        builder.Services.AddSingleton<IHostedService, TokenCleanupHost>();

        return builder;
    }

    /// <summary>
    /// Adds an implementation of the IOperationalStoreNotification to IdentityServer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    public static IIdentityServerBuilder AddOperationalStoreNotification<T>(
        this IIdentityServerBuilder builder)
        where T : class, IOperationalStoreNotification
    {
        builder.Services.AddOperationalStoreNotification<T>();
        return builder;
    }
}
