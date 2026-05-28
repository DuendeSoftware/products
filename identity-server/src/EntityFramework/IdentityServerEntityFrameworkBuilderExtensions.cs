// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Services;
using Duende.IdentityServer.EntityFramework.Storage;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to add EF database support to IdentityServer.
/// </summary>
public static class IdentityServerEntityFrameworkBuilderExtensions
{
    /// <summary>
    /// Registers Entity Framework Core implementations of <see cref="IClientStore"/>, <see cref="IResourceStore"/>,
    /// <see cref="ICorsPolicyService"/>, <see cref="IIdentityProviderStore"/>, and the SAML service provider store
    /// backed by the default <see cref="ConfigurationDbContext"/>. Use this to persist client, resource, and
    /// identity provider configuration in a relational database.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the configuration store to.</param>
    /// <param name="storeOptionsAction">An optional delegate to configure <see cref="ConfigurationStoreOptions"/>,
    /// such as the EF Core database provider and table name prefixes.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddConfigurationStore(
        this IIdentityServerBuilder builder,
        Action<ConfigurationStoreOptions>? storeOptionsAction = null) => builder.AddConfigurationStore<ConfigurationDbContext>(storeOptionsAction);

    /// <summary>
    /// Registers Entity Framework Core implementations of <see cref="IClientStore"/>, <see cref="IResourceStore"/>,
    /// <see cref="ICorsPolicyService"/>, <see cref="IIdentityProviderStore"/>, and the SAML service provider store
    /// backed by a custom <typeparamref name="TContext"/>. Use this to persist client, resource, and identity provider
    /// configuration in a relational database with a custom DbContext.
    /// </summary>
    /// <typeparam name="TContext">The custom <see cref="IConfigurationDbContext"/> DbContext type to use.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the configuration store to.</param>
    /// <param name="storeOptionsAction">An optional delegate to configure <see cref="ConfigurationStoreOptions"/>,
    /// such as the EF Core database provider and table name prefixes.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
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
        builder.AddSamlServiceProviderStore<SamlServiceProviderStore>();

        return builder;
    }

    /// <summary>
    /// Registers caching decorators for the EF-backed <see cref="IClientStore"/>, <see cref="IResourceStore"/>,
    /// <see cref="ICorsPolicyService"/>, and <see cref="IIdentityProviderStore"/> implementations.
    /// Reduces database round-trips by caching configuration data in memory. Cache durations are
    /// configurable via <c>IdentityServerOptions.Caching</c>. Call this after <see cref="AddConfigurationStore(IIdentityServerBuilder, Action{ConfigurationStoreOptions}?)"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add caching to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
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
    /// Registers Entity Framework Core implementations of <see cref="IPersistedGrantStore"/>, <see cref="ISigningKeyStore"/>,
    /// <see cref="IDeviceFlowStore"/>, server-side session store, and pushed authorization request store backed by the
    /// default <see cref="PersistedGrantDbContext"/>. Also registers a hosted service for automatic token cleanup.
    /// Use this to persist grants, refresh tokens, and other operational data in a relational database.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the operational store to.</param>
    /// <param name="storeOptionsAction">An optional delegate to configure <see cref="OperationalStoreOptions"/>,
    /// such as the EF Core database provider, table name prefixes, and token cleanup settings.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddOperationalStore(
        this IIdentityServerBuilder builder,
        Action<OperationalStoreOptions>? storeOptionsAction = null) => builder.AddOperationalStore<PersistedGrantDbContext>(storeOptionsAction);

    /// <summary>
    /// Registers Entity Framework Core implementations of <see cref="IPersistedGrantStore"/>, <see cref="ISigningKeyStore"/>,
    /// <see cref="IDeviceFlowStore"/>, server-side session store, and pushed authorization request store backed by a
    /// custom <typeparamref name="TContext"/>. Also registers a hosted service for automatic token cleanup.
    /// Use this to persist grants, refresh tokens, and other operational data in a relational database with a custom DbContext.
    /// </summary>
    /// <typeparam name="TContext">The custom <see cref="IPersistedGrantDbContext"/> DbContext type to use.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the operational store to.</param>
    /// <param name="storeOptionsAction">An optional delegate to configure <see cref="OperationalStoreOptions"/>,
    /// such as the EF Core database provider, table name prefixes, and token cleanup settings.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
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
        builder.AddSamlSigninStateStore<SamlSigninStateStore>();
        builder.Services.TryAddSingleton<ISamlSigninStateSerializer, DefaultSamlSigninStateSerializer>();
        builder.AddSamlLogoutSessionStore<SamlLogoutSessionStore>();

        builder.Services.AddSingleton<IHostedService, TokenCleanupHost>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IOperationalStoreNotification"/> implementation that receives
    /// notifications when expired grants and tokens are removed during the automatic token cleanup process.
    /// </summary>
    /// <typeparam name="T">The <see cref="IOperationalStoreNotification"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the notification handler to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddOperationalStoreNotification<T>(
        this IIdentityServerBuilder builder)
        where T : class, IOperationalStoreNotification
    {
        builder.Services.AddOperationalStoreNotification<T>();
        return builder;
    }
}
