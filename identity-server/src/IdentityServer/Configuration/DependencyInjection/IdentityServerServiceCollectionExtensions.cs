// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI extension methods for adding IdentityServer
/// </summary>
public static class IdentityServerServiceCollectionExtensions
{
    /// <summary>
    /// Creates a builder.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityServerBuilder(this IServiceCollection services) => new IdentityServerBuilder(services);

    /// <summary>
    /// Adds IdentityServer.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityServer(this IServiceCollection services)
    {
        var builder = services.AddIdentityServerBuilder();

        _ = builder
            .AddRequiredPlatformServices()
            .AddCookieAuthentication()
            .AddCoreServices()
            .AddDefaultEndpoints()
            .AddPluggableServices()
            .AddKeyManagement()
            .AddDynamicProvidersCore()
            .AddOidcDynamicProvider()
            .AddValidators()
            .AddResponseGenerators()
            .AddDefaultSecretParsers()
            .AddDefaultSecretValidators();

        // provide default in-memory implementations, not suitable for most production scenarios
        _ = builder.AddInMemoryPersistedGrants();
        _ = builder.AddInMemoryPushedAuthorizationRequests();

        return builder;
    }

    /// <summary>
    /// Adds IdentityServer.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="setupAction">The setup action.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityServer(this IServiceCollection services, Action<IdentityServerOptions> setupAction)
    {
        _ = services.Configure(setupAction);
        return services.AddIdentityServer();
    }

    /// <summary>
    /// Adds the IdentityServer.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityServer(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.Configure<IdentityServerOptions>(configuration);
        return services.AddIdentityServer();
    }

    /// <summary>
    /// Configures the OpenIdConnect handlers to persist the state parameter into the server-side IDistributedCache.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="schemes">The schemes to configure. If none provided, then all OpenIdConnect schemes will use the cache.</param>
    public static IServiceCollection AddOidcStateDataFormatterCache(this IServiceCollection services, params string[] schemes)
    {
        _ = services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>>(
            svcs => new ConfigureOpenIdConnectOptions(
                schemes,
                svcs)
        );

        return services;
    }
}
