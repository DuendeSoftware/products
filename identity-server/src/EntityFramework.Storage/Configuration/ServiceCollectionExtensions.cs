// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.EntityFramework.Storage;

/// <summary>
/// Extension methods to add EF database support to IdentityServer.
/// </summary>
public static class IdentityServerEntityFrameworkBuilderExtensions
{
    /// <summary>
    /// Add Configuration DbContext to the DI system.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="storeOptionsAction">The store options action.</param>
    public static IServiceCollection AddConfigurationDbContext(this IServiceCollection services,
        Action<ConfigurationStoreOptions>? storeOptionsAction = null) => services.AddConfigurationDbContext<ConfigurationDbContext>(storeOptionsAction);

    /// <summary>
    /// Add Configuration DbContext to the DI system.
    /// </summary>
    /// <typeparam name="TContext">The IConfigurationDbContext to use.</typeparam>
    /// <param name="services"></param>
    /// <param name="storeOptionsAction">The store options action.</param>
    public static IServiceCollection AddConfigurationDbContext<TContext>(this IServiceCollection services,
        Action<ConfigurationStoreOptions>? storeOptionsAction = null)
        where TContext : DbContext, IConfigurationDbContext
    {
        var options = new ConfigurationStoreOptions();
        services.AddSingleton(options);
        storeOptionsAction?.Invoke(options);

        if (options.ResolveDbContextOptions != null)
        {
            if (options.EnablePooling)
            {
                if (options.PoolSize.HasValue)
                {
                    services.AddDbContextPool<TContext>(options.ResolveDbContextOptions, options.PoolSize.Value);
                }
                else
                {
                    services.AddDbContextPool<TContext>(options.ResolveDbContextOptions);
                }
            }
            else
            {
                services.AddDbContext<TContext>(options.ResolveDbContextOptions);
            }
        }
        else
        {
            if (options.EnablePooling)
            {
                if (options.PoolSize.HasValue)
                {
                    services.AddDbContextPool<TContext>(
                        dbCtxBuilder => { options.ConfigureDbContext?.Invoke(dbCtxBuilder); }, options.PoolSize.Value);
                }
                else
                {
                    services.AddDbContextPool<TContext>(
                        dbCtxBuilder => { options.ConfigureDbContext?.Invoke(dbCtxBuilder); });
                }
            }
            else
            {
                services.AddDbContext<TContext>(dbCtxBuilder =>
                {
                    options.ConfigureDbContext?.Invoke(dbCtxBuilder);
                });
            }
        }

        services.AddScoped<IConfigurationDbContext>(svcs => svcs.GetRequiredService<TContext>());

        return services;
    }

    /// <summary>
    /// Adds operational DbContext to the DI system.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="storeOptionsAction">The store options action.</param>
    public static IServiceCollection AddOperationalDbContext(this IServiceCollection services,
        Action<OperationalStoreOptions>? storeOptionsAction = null) => services.AddOperationalDbContext<PersistedGrantDbContext>(storeOptionsAction);

    /// <summary>
    /// Adds operational DbContext to the DI system.
    /// </summary>
    /// <typeparam name="TContext">The IPersistedGrantDbContext to use.</typeparam>
    /// <param name="services"></param>
    /// <param name="storeOptionsAction">The store options action.</param>
    public static IServiceCollection AddOperationalDbContext<TContext>(this IServiceCollection services,
        Action<OperationalStoreOptions>? storeOptionsAction = null)
        where TContext : DbContext, IPersistedGrantDbContext
    {
        var storeOptions = new OperationalStoreOptions();
        services.AddSingleton(storeOptions);
        storeOptionsAction?.Invoke(storeOptions);

        if (storeOptions.ResolveDbContextOptions != null)
        {
            if (storeOptions.EnablePooling)
            {
                if (storeOptions.PoolSize.HasValue)
                {
                    services.AddDbContextPool<TContext>(storeOptions.ResolveDbContextOptions,
                        storeOptions.PoolSize.Value);
                }
                else
                {
                    services.AddDbContextPool<TContext>(storeOptions.ResolveDbContextOptions);
                }
            }
            else
            {
                services.AddDbContext<TContext>(storeOptions.ResolveDbContextOptions);
            }
        }
        else
        {
            if (storeOptions.EnablePooling)
            {
                if (storeOptions.PoolSize.HasValue)
                {
                    services.AddDbContextPool<TContext>(
                        dbCtxBuilder => { storeOptions.ConfigureDbContext?.Invoke(dbCtxBuilder); },
                        storeOptions.PoolSize.Value);
                }
                else
                {
                    services.AddDbContextPool<TContext>(
                        dbCtxBuilder => { storeOptions.ConfigureDbContext?.Invoke(dbCtxBuilder); });
                }
            }
            else
            {
                services.AddDbContext<TContext>(dbCtxBuilder =>
                {
                    storeOptions.ConfigureDbContext?.Invoke(dbCtxBuilder);
                });
            }
        }

        services.AddScoped<IPersistedGrantDbContext>(svcs => svcs.GetRequiredService<TContext>());
        services.AddTransient<ITokenCleanupService, TokenCleanupService>();

        return services;
    }

    /// <summary>
    /// Adds an implementation of the IOperationalStoreNotification to the DI system.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    public static IServiceCollection AddOperationalStoreNotification<T>(this IServiceCollection services)
        where T : class, IOperationalStoreNotification
    {
        services.AddTransient<IOperationalStoreNotification, T>();
        return services;
    }
}
