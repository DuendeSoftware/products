// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding session management
/// </summary>
public static class SessionManagementServiceCollectionExtensions
{
    /// <summary>
    /// Adds a server-side session store using the provided store type
    /// </summary>
    /// <returns></returns>
    public static IIdentityServerBuilder AddServerSideSessions<T>(this IIdentityServerBuilder builder)
        where T : class, IServerSideSessionStore
    {
        // the order of these two calls is important
        return builder
            .AddServerSideSessionStore<T>()
            .AddServerSideSessions();
    }

    /// <summary>
    /// Adds a server-side session store using the in-memory store
    /// </summary>
    /// <returns></returns>
    public static IIdentityServerBuilder AddServerSideSessions(this IIdentityServerBuilder builder)
    {
        builder.Services.AddSingleton<IServerSideSessionsMarker, NopIServerSideSessionsMarker>();
        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureApplicationCookieTicketStore>();
        builder.Services.TryAddTransient<ISessionManagementService, DefaultSessionManagementService>();
        builder.Services.TryAddTransient<IServerSideTicketStore, ServerSideTicketStore>();

        // wraps IRefreshTokenService to extend sessions
        builder.Services.AddTransientDecorator<IRefreshTokenService, ServerSideSessionRefreshTokenService>();

        builder.Services.AddSingleton<IHostedService, ServerSideSessionCleanupHost>();

        // only add if not already in DI
        builder.Services.TryAddSingleton<IServerSideSessionStore, InMemoryServerSideSessionStore>();

        return builder;
    }

    ///// <summary>
    ///// Adds a server-side sessions for the scheme specified.
    ///// Typically used to add server sessions for additional schemes beyond the default cookie handler.
    ///// This requires AddServerSideSessions to have also been configured on the IdentityServerBuilder.
    ///// </summary>
    ///// <returns></returns>
    // TODO: do we want to support other schemes?
    //public static IIdentityServerBuilder AddServerSideSessionsForScheme(this IIdentityServerBuilder builder, string scheme)
    //{
    //    ArgumentNullException.ThrowIfNull(scheme);

    //    builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(svcs => new PostConfigureApplicationCookieTicketStore(svcs.GetRequiredService<IHttpContextAccessor>(), scheme));
    //    return builder;
    //}

    /// <summary>
    /// Adds a server-side session store using the supplied session store implementation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IIdentityServerBuilder AddServerSideSessionStore<T>(this IIdentityServerBuilder builder)
        where T : class, IServerSideSessionStore
    {
        builder.Services.AddTransient<IServerSideSessionStore>(svcs =>
        {
            // odd that this Func is marked as non-null return value, since it is allowed to return null here.
            if (svcs.GetService<IServerSideSessionsMarker>() == null)
            {
                return null!;
            }

            return svcs.GetRequiredService<T>();
        });

        builder.Services.AddTransient<T>();

        return builder;
    }
}
