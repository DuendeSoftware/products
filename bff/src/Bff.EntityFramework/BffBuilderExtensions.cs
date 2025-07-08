// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Builder;
using Duende.Bff.EntityFramework.Internal;
using Duende.Bff.SessionManagement.SessionStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.Bff.EntityFramework;

/// <summary>
/// Extensions for BffBuilder
/// </summary>
public static class BffBuilderExtensions
{
    /// <summary>
    /// Adds entity framework core support for user session store.
    /// </summary>
    /// <param name="bffBuilder"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T AddEntityFrameworkServerSideSessions<T>(this T bffBuilder, Action<IServiceProvider, DbContextOptionsBuilder> action)
        where T : IBffServicesBuilder
        => bffBuilder.AddEntityFrameworkServerSideSessions<SessionDbContext, T>(action);

    /// <summary>
    /// Adds entity framework core support for user session store.
    /// </summary>
    /// <param name="bffBuilder"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T AddEntityFrameworkServerSideSessions<T>(this T bffBuilder, Action<DbContextOptionsBuilder> action)
        where T : IBffServicesBuilder
        => bffBuilder.AddEntityFrameworkServerSideSessions<SessionDbContext, T>(action);

    /// <summary>
    /// Adds entity framework core support for user session store.
    /// </summary>
    /// <param name="bffBuilder"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T AddEntityFrameworkServerSideSessions<TContext, T>(this T bffBuilder, Action<IServiceProvider, DbContextOptionsBuilder> action)
        where TContext : DbContext, ISessionDbContext
        where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(bffBuilder);
        bffBuilder.Services.AddDbContext<TContext>(action);
        return bffBuilder.AddEntityFrameworkServerSideSessionsServices<TContext, T>();
    }

    /// <summary>
    /// Adds entity framework core support for user session store.
    /// </summary>
    /// <param name="bffBuilder"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T AddEntityFrameworkServerSideSessions<TContext, T>(this T bffBuilder, Action<DbContextOptionsBuilder> action)
        where TContext : DbContext, ISessionDbContext
        where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(bffBuilder);
        bffBuilder.Services.AddDbContext<TContext>(action);
        return bffBuilder.AddEntityFrameworkServerSideSessionsServices<TContext, T>();
    }

    /// <summary>
    /// Adds entity framework core support for user session store, but does not register a DbContext.
    /// Use this API to register the BFF Entity Framework services when you plan to register your own DbContext (e.g. with AddDbContextPool).
    /// </summary>
    /// <param name="bffBuilder"></param>
    /// <returns></returns>
    public static T AddEntityFrameworkServerSideSessionsServices<TContext, T>(this T bffBuilder)
        where TContext : ISessionDbContext
        where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(bffBuilder);
        bffBuilder.Services.AddTransient<IUserSessionStoreCleanup, UserSessionStore>();
        bffBuilder.Services.AddTransient<ISessionDbContext>(svcs => svcs.GetRequiredService<TContext>());
        bffBuilder.AddServerSideSessions<UserSessionStore>();

        return bffBuilder;
    }

    /// <summary>
    /// Allows configuring the SessionStoreOptions.
    /// </summary>
    public static T ConfigureEntityFrameworkSessionStoreOptions<T>(this T bffBuilder, Action<SessionStoreOptions> action)
        where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(bffBuilder);
        bffBuilder.Services.Configure(action);
        return bffBuilder;
    }

    public static T AddSessionCleanupBackgroundProcess<T>(this T bffBuilder)
        where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(bffBuilder);
        bffBuilder.Services.AddSingleton<IHostedService, SessionCleanupHost>();
        return bffBuilder;
    }

}

