// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Blazor.Client.Internals;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Blazor.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Duende.BFF services to a Blazor Client (wasm) application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureAction">A callback used to set <see cref="BffBlazorOptions"/>.</param>
    public static IServiceCollection AddBffBlazorClient(this IServiceCollection services,
        Action<BffBlazorOptions>? configureAction = null)
    {
        if (configureAction != null)
        {
            services.Configure(configureAction);
        }

        services
            .AddAuthorizationCore()
            // Most services for wasm are singletons, because DI scope doesn't exist in wasm
            .AddSingleton<IPersistentUserService, PersistentUserService>()
            .AddSingleton<IGetUserService, GetUserService>()
            .AddSingleton<AuthenticationStateProvider, BffClientAuthenticationStateProvider>()
            .AddSingleton(TimeProvider.System)
            // HttpMessageHandlers must be registered as transient
            .AddTransient<AntiforgeryHandler>() 
            .AddHttpClient(BffClientAuthenticationStateProvider.HttpClientName, (sp, client) =>
            {
                var baseAddress = GetStateProviderBaseAddress(sp);
                client.BaseAddress = new Uri(baseAddress);
            }).AddHttpMessageHandler<AntiforgeryHandler>();

        return services;
    }

    private static string GetStateProviderBaseAddress(IServiceProvider sp)
    {
        var opt = sp.GetRequiredService<IOptions<BffBlazorOptions>>();
        if (opt.Value.StateProviderBaseAddress != null)
        {
            return opt.Value.StateProviderBaseAddress;
        }
        else
        {
            var hostEnv = sp.GetRequiredService<IWebAssemblyHostEnvironment>();
            return hostEnv.BaseAddress;
        }
    }

    private static string GetBaseAddress(IServiceProvider sp)
    {
        var opt = sp.GetRequiredService<IOptions<BffBlazorOptions>>();
        if (opt.Value.RemoteApiBaseAddress != null)
        {
            return opt.Value.RemoteApiBaseAddress;
        }
        else
        {
            var hostEnv = sp.GetRequiredService<IWebAssemblyHostEnvironment>();
            return hostEnv.BaseAddress;
        }
    }

    private static string GetRemoteApiPath(IServiceProvider sp)
    {
        var opt = sp.GetRequiredService<IOptions<BffBlazorOptions>>();
        return opt.Value.RemoteApiPath;
    }

    private static Action<IServiceProvider, HttpClient> SetBaseAddress(
        Action<IServiceProvider, HttpClient>? configureClient)
    {
        return (sp, client) =>
        {
            SetBaseAddress(sp, client);
            configureClient?.Invoke(sp, client);
        };
    }

    private static Action<IServiceProvider, HttpClient> SetBaseAddress(
        Action<HttpClient>? configureClient)
    {
        return (sp, client) =>
        {
            SetBaseAddress(sp, client);
            configureClient?.Invoke(client);
        };
    }

    private static void SetBaseAddress(IServiceProvider sp, HttpClient client)
    {
        var baseAddress = GetBaseAddress(sp);
        if (!baseAddress.EndsWith("/"))
        {
            baseAddress += "/";
        }

        var remoteApiPath = GetRemoteApiPath(sp);
        if (!string.IsNullOrEmpty(remoteApiPath))
        {
            if (remoteApiPath.StartsWith("/"))
            {
                remoteApiPath = remoteApiPath.Substring(1);
            }

            if (!remoteApiPath.EndsWith("/"))
            {
                remoteApiPath += "/";
            }
        }

        client.BaseAddress = new Uri(new Uri(baseAddress), remoteApiPath);
    }

    /// <summary>
    /// Adds a named <see cref="HttpClient"/> for use when invoking remote APIs
    /// proxied through Duende.Bff and configures the client with a callback.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clientName">The name of that <see cref="HttpClient"/> to
    /// configure. A common use case is to use the same named client in multiple
    /// render contexts that are automatically switched between via interactive
    /// render modes. In that case, ensure both the client and server project
    /// define the HttpClient appropriately.</param>
    /// <param name="configureClient">A configuration callback used to set up
    /// the <see cref="HttpClient"/>.</param>
    public static IHttpClientBuilder AddRemoteApiHttpClient(this IServiceCollection services, string clientName,
        Action<HttpClient> configureClient)
    {
        return services.AddHttpClient(clientName, SetBaseAddress(configureClient))
            .AddHttpMessageHandler<AntiforgeryHandler>();
    }

    /// <summary>
    /// Adds a named <see cref="HttpClient"/> for use when invoking remote APIs
    /// proxied through Duende.Bff and configures the client with a callback
    /// that has access to the underlying service provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clientName">The name of that <see cref="HttpClient"/> to
    /// configure. A common use case is to use the same named client in multiple
    /// render contexts that are automatically switched between via interactive
    /// render modes. In that case, ensure both the client and server project
    /// define the HttpClient appropriately.</param>
    /// <param name="configureClient">A configuration callback used to set up
    /// the <see cref="HttpClient"/>.</param>
    public static IHttpClientBuilder AddRemoteApiHttpClient(this IServiceCollection services, string clientName,
        Action<IServiceProvider, HttpClient>? configureClient = null)
    {
        return services.AddHttpClient(clientName, SetBaseAddress(configureClient))
            .AddHttpMessageHandler<AntiforgeryHandler>();
    }

    /// <summary>
    /// Adds a typed <see cref="HttpClient"/> for use when invoking remote APIs
    /// proxied through Duende.Bff and configures the client with a callback.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">A configuration callback used to set up
    /// the <see cref="HttpClient"/>.</param>
    public static IHttpClientBuilder AddRemoteApiHttpClient<T>(this IServiceCollection services,
        Action<HttpClient> configureClient)
        where T : class
    {
        return services.AddHttpClient<T>(SetBaseAddress(configureClient))
            .AddHttpMessageHandler<AntiforgeryHandler>();
    }

    /// <summary>
    /// Adds a typed <see cref="HttpClient"/> for use when invoking remote APIs
    /// proxied through Duende.Bff and configures the client with a callback
    /// that has access to the underlying service provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">A configuration callback used to set up
    /// the <see cref="HttpClient"/>.</param>
    public static IHttpClientBuilder AddRemoteApiHttpClient<T>(this IServiceCollection services,
        Action<IServiceProvider, HttpClient>? configureClient = null)
        where T : class
    {
        return services.AddHttpClient<T>(SetBaseAddress(configureClient))
            .AddHttpMessageHandler<AntiforgeryHandler>();
    }
}