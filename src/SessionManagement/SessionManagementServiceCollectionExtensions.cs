// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.SessionManagement;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        where T : class, IUserSessionStore
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
        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureApplicationCookieTicketStore>();
        builder.Services.TryAddTransient<IServerSideTicketStore, ServerSideTicketStore>();

        // only add if not already in DI
        builder.Services.TryAddSingleton<IUserSessionStore, InMemoryUserSessionStore>();

        return builder;
    }

    /// <summary>
    /// Adds a server-side sessions for the scheme specified.
    /// Typically used to add server sessions for additional schemes beyond the default cookie handler.
    /// This requires AddServerSideSessions to have also been configured on the IdentityServerBuilder.
    /// </summary>
    /// <returns></returns>
    public static IIdentityServerBuilder AddServerSideSessionsForScheme(this IIdentityServerBuilder builder, string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(svcs => new PostConfigureApplicationCookieTicketStore(svcs.GetRequiredService<IHttpContextAccessor>(), scheme));
        return builder;
    }

    /// <summary>
    /// Adds a server-side session store using the supplied session store implementation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IIdentityServerBuilder AddServerSideSessionStore<T>(this IIdentityServerBuilder builder)
        where T : class, IUserSessionStore
    {
        builder.Services.AddTransient<IUserSessionStore, T>();
        return builder;
    }
}