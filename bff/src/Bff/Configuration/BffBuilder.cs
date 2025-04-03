// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Encapsulates DI options for Duende.BFF
/// </summary>
public class BffBuilder(IServiceCollection services)
{
    /// <summary>
    /// The service collection
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Adds a server-side session store using the in-memory store
    /// </summary>
    /// <returns></returns>
    public BffBuilder AddServerSideSessions()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureApplicationCookieTicketStore>();
        Services.AddTransient<IServerTicketStore, ServerSideTicketStore>();
        Services.AddTransient<ISessionRevocationService, SessionRevocationService>();
        Services.AddSingleton<IHostedService, SessionCleanupHost>();

        // only add if not already in DI
        Services.TryAddSingleton<IUserSessionStore, InMemoryUserSessionStore>();

#pragma warning restore CS0618 // Type or member is obsolete

        return this;
    }

    /// <summary>
    /// Adds a server-side session store using the supplied session store implementation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public BffBuilder AddServerSideSessions<T>()
        where T : class, IUserSessionStore
    {
        Services.AddTransient<IUserSessionStore, T>();
        return AddServerSideSessions();
    }
}
