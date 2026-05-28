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
    /// Creates an <see cref="IIdentityServerBuilder"/> that wraps the given service collection,
    /// providing a fluent API for further IdentityServer configuration.
    /// </summary>
    /// <param name="services">The ASP.NET Core service collection to wrap.</param>
    /// <returns>An <see cref="IIdentityServerBuilder"/> for chaining additional IdentityServer configuration.</returns>
    public static IIdentityServerBuilder AddIdentityServerBuilder(this IServiceCollection services) => new IdentityServerBuilder(services);

    /// <summary>
    /// Registers IdentityServer and all of its required services into the ASP.NET Core dependency injection container.
    /// This includes platform services, cookie authentication, core services, all protocol endpoints,
    /// pluggable services (token creation, consent, events, etc.), automatic key management,
    /// dynamic external identity providers, request validators, response generators, and default
    /// secret parsers and validators. In-memory stores for persisted grants and pushed authorization
    /// requests are registered as defaults; replace them with durable implementations for production.
    /// </summary>
    /// <param name="services">The ASP.NET Core service collection to register IdentityServer services into.</param>
    /// <returns>An <see cref="IIdentityServerBuilder"/> for chaining additional IdentityServer configuration such as
    /// signing credentials, client/resource stores, and custom services.</returns>
    public static IIdentityServerBuilder AddIdentityServer(this IServiceCollection services)
    {
        var builder = services.AddIdentityServerBuilder();

        builder
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
        builder.AddInMemoryPersistedGrants();
        builder.AddInMemoryPushedAuthorizationRequests();

        return builder;
    }

    /// <summary>
    /// Registers IdentityServer and all of its required services into the ASP.NET Core dependency injection container,
    /// applying the provided configuration delegate to <see cref="IdentityServerOptions"/> before registration.
    /// </summary>
    /// <param name="services">The ASP.NET Core service collection to register IdentityServer services into.</param>
    /// <param name="setupAction">A delegate used to configure <see cref="IdentityServerOptions"/>, such as setting
    /// the issuer URI, enabling CORS, customizing endpoint paths, or adjusting token lifetimes.</param>
    /// <returns>An <see cref="IIdentityServerBuilder"/> for chaining additional IdentityServer configuration such as
    /// signing credentials, client/resource stores, and custom services.</returns>
    public static IIdentityServerBuilder AddIdentityServer(this IServiceCollection services, Action<IdentityServerOptions> setupAction)
    {
        services.Configure(setupAction);
        return services.AddIdentityServer();
    }

    /// <summary>
    /// Registers IdentityServer and all of its required services into the ASP.NET Core dependency injection container,
    /// binding <see cref="IdentityServerOptions"/> from the provided <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The ASP.NET Core service collection to register IdentityServer services into.</param>
    /// <param name="configuration">An <see cref="IConfiguration"/> section (e.g. from <c>appsettings.json</c>)
    /// whose values are bound to <see cref="IdentityServerOptions"/>.</param>
    /// <returns>An <see cref="IIdentityServerBuilder"/> for chaining additional IdentityServer configuration such as
    /// signing credentials, client/resource stores, and custom services.</returns>
    public static IIdentityServerBuilder AddIdentityServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentityServerOptions>(configuration);
        return services.AddIdentityServer();
    }

    /// <summary>
    /// Configures the OpenIdConnect handlers to persist the state parameter into the server-side IDistributedCache.
    /// This prevents the state cookie from growing too large when using external identity providers.
    /// </summary>
    /// <param name="services">The ASP.NET Core service collection to configure.</param>
    /// <param name="schemes">The OpenIdConnect authentication scheme names to configure. If none are provided,
    /// all registered OpenIdConnect schemes will use the distributed cache for state storage.</param>
    public static IServiceCollection AddOidcStateDataFormatterCache(this IServiceCollection services, params string[] schemes)
    {
        services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>>(
            svcs => new ConfigureOpenIdConnectOptions(
                schemes,
                svcs)
        );

        return services;
    }
}
