// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Encapsulates DI options for Duende.BFF
/// </summary>
public class BffBuilder
{
    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="services"></param>
    public BffBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// The service collection
    /// </summary>
    public IServiceCollection Services { get; }
        
    /// <summary>
    /// Adds a server-side session store using the in-memory store
    /// </summary>
    /// <returns></returns>
    public BffBuilder AddServerSideSessions()
    {
        Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureApplicationCookieTicketStore>();
        Services.AddTransient<IServerTicketStore, ServerSideTicketStore>();
        Services.AddTransient<ISessionRevocationService, SessionRevocationService>();
        Services.AddSingleton<IHostedService, SessionCleanupHost>();


        // only add if not already in DI
        Services.TryAddSingleton<IUserSessionStore, InMemoryUserSessionStore>();

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