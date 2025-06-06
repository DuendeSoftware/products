// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.SessionManagement.Configuration;
using Duende.Bff.SessionManagement.Revocation;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Encapsulates DI options for Duende.BFF
/// </summary>
public sealed class BffBuilder(IServiceCollection services)
{
    /// <summary>
    /// The service collection
    /// </summary>
    public IServiceCollection Services { get; } = services;

    public BffBuilder WithDefaultOpenIdConnectOptions(Action<OpenIdConnectOptions> oidc)
    {
        Services.Configure<BffOptions>(bffOptions => bffOptions.ConfigureOpenIdConnectDefaults += oidc);
        return this;
    }

    public BffBuilder WithDefaultCookieOptions(Action<CookieAuthenticationOptions> oidc)
    {
        Services.Configure<BffOptions>(bffOptions => bffOptions.ConfigureCookieDefaults += oidc);
        return this;
    }


    internal BffBuilder AddDynamicFrontends()
    {
        Services.AddHybridCache();

        Services.AddTransient<IStartupFilter, ConfigureBffStartupFilter>();

        // Register the frontend collection, which will be used to store and retrieve frontends
        Services.AddSingleton<FrontendCollection>();
        // Add a public accessible interface to the frontend collection, so our users can access it
        Services.AddSingleton<IFrontendCollection>((sp) => sp.GetRequiredService<FrontendCollection>());

        Services.AddTransient<SelectedFrontend>();
        Services.AddTransient<FrontendSelector>();

        // Add a scheme provider that will inject authentication schemes that are needed for the BFF
        Services.AddTransient<IAuthenticationSchemeProvider, BffAuthenticationSchemeProvider>();

        // Configure the AspNet Core Authentication settings if no 
        // .AddAuthentication().AddCookie().AddOpenIdConnect() was added
        Services.AddSingleton<IPostConfigureOptions<AuthenticationOptions>, BffConfigureAuthenticationOptions>();

        Services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, BffConfigureCookieOptions>();

        Services.AddHttpContextAccessor();

        // Add 'default' configure methods that would have been added by
        // .AddAuthentication().AddCookie().AddOpenIdConnect()
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>());
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureCookieAuthenticationOptions>());

        Services.AddTransient<PathMapper>();

        Services.TryAddSingleton<IIndexHtmlClient, IndexHtmlHttpClient>();

        var indexHtmlClientBuilder = Services.AddHttpClient(Constants.HttpClientNames.IndexHtmlHttpClient);

        // Todo: factor this to an extension method
        Services.Configure<HttpClientFactoryOptions>(indexHtmlClientBuilder.Name, options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(httpMessageHandlerBuilder =>
            {
                var defaults = httpMessageHandlerBuilder.Services.GetRequiredService<IOptions<BffOptions>>();
                if (defaults.Value.BackchannelMessageHandler != null)
                {
                    httpMessageHandlerBuilder.PrimaryHandler = defaults.Value.BackchannelMessageHandler;
                }
            });
        });

        // Register an 'disabled' remote route handler, that can be replaced by calling
        // .AddRemoteApis() from BFF.Yarp
        Services.TryAddSingleton<IRemoteRouteHandler, RemoteRouteHandlingDisabled>();

        return this;
    }

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

    public BffBuilder LoadConfiguration(IConfiguration section)
    {
        Services.Configure<BffConfiguration>(section);
        return this;
    }

    public BffBuilder AddFrontends(params BffFrontend[] frontends)
    {
        // Check for duplicate frontend names
        var duplicateNames = frontends
            .GroupBy(f => f.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNames.Any())
        {
            throw new InvalidOperationException($"Duplicate frontend names detected: {string.Join(", ", duplicateNames.Select(n => n))}");
        }

        foreach (var frontend in frontends)
        {
            Services.Add(new ServiceDescriptor(typeof(BffFrontend), frontend));
        }

        return this;
    }
}
