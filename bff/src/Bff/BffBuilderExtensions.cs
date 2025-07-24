// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Builder;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Endpoints;
using Duende.Bff.Endpoints.Internal;
using Duende.Bff.Internal;
using Duende.Bff.Licensing;
using Duende.Bff.Otel;
using Duende.Bff.SessionManagement.Configuration;
using Duende.Bff.SessionManagement.Revocation;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Duende.Private.Licensing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

public static class BffBuilderExtensions
{
    public static T ConfigureOpenIdConnect<T>(this T builder, Action<OpenIdConnectOptions> oidc)
        where T : IBffBuilder
    {
        builder.Services.Configure<BffOptions>(bffOptions => bffOptions.ConfigureOpenIdConnectDefaults += oidc);
        return builder;
    }

    public static T ConfigureCookies<T>(this T builder, Action<CookieAuthenticationOptions> oidc)
        where T : IBffBuilder
    {
        builder.Services.Configure<BffOptions>(bffOptions => bffOptions.ConfigureCookieDefaults += oidc);
        return builder;
    }

    internal static T AddBaseBffServices<T>(this T builder) where T : IBffServicesBuilder
    {
        builder.Services.AddSingleton<GetLicenseKey>(sp => () => sp.GetRequiredService<IOptions<BffOptions>>().Value.LicenseKey);
        builder.Services.AddSingleton<LicenseAccessor<BffLicense>>();
        builder.Services.AddSingleton<LicenseValidator>();
        builder.Services.AddDistributedMemoryCache();
        // IMPORTANT: The BffConfigureOpenIdConnectOptions MUST be called before calling
        // AddOpenIdConnectAccessTokenManagement because both configure the same options
        // The AddOpenIdConnectAccessTokenManagement adds OR wraps the BackchannelHttpHandler
        // to add DPoP support. However, our code can also add a backchannel handler. 
        builder.Services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, BffConfigureOpenIdConnectOptions>();
        builder.Services.AddOpenIdConnectAccessTokenManagement();

        builder.Services.AddSingleton<IConfigureOptions<UserTokenManagementOptions>, ConfigureUserTokenManagementOptions>();

        builder.Services.AddTransient<IReturnUrlValidator, LocalUrlReturnUrlValidator>();
        builder.Services.TryAddSingleton<IAccessTokenRetriever, DefaultAccessTokenRetriever>();

        // management endpoints
        builder.Services.AddTransient<ILoginEndpoint, DefaultLoginEndpoint>();
#pragma warning disable CS0618 // Type or member is obsolete
        builder.Services.AddTransient<ISilentLoginEndpoint, DefaultSilentLoginEndpoint>();
#pragma warning restore CS0618 // Type or member is obsolete
        builder.Services.AddTransient<ISilentLoginCallbackEndpoint, DefaultSilentLoginCallbackEndpoint>();
        builder.Services.AddTransient<ILogoutEndpoint, DefaultLogoutEndpoint>();
        builder.Services.AddTransient<IUserEndpoint, DefaultUserEndpoint>();
        builder.Services.AddTransient<IBackchannelLogoutEndpoint, DefaultBackchannelLogoutEndpoint>();
        builder.Services.AddTransient<IDiagnosticsEndpoint, DefaultDiagnosticsEndpoint>();

        // session management
        builder.Services.TryAddTransient<ISessionRevocationService, NopSessionRevocationService>();

        // cookie configuration
        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureSlidingExpirationCheck>();
        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureApplicationCookieRevokeRefreshToken>();
        builder.Services.AddSingleton<ActiveCookieAuthenticationScheme>();
        builder.Services.AddSingleton<ActiveOpenIdConnectAuthenticationScheme>();

        builder.Services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, PostConfigureOidcOptionsForSilentLogin>();

        AddBffMetrics(builder);

        // wrap ASP.NET Core
        builder.Services.AddAuthentication();
        builder.Services.AddTransientDecorator<IAuthenticationService, BffAuthenticationService>();

        // Make sure the session partitioning is registered. There are a few codepaths that require this injected
        // even if you are not using session management.
        builder.Services.AddSingleton<BuildUserSessionPartitionKey>(sp => sp.GetRequiredService<UserSessionPartitionKeyBuilder>().BuildPartitionKey);
        builder.Services.AddSingleton<UserSessionPartitionKeyBuilder>();

        return builder;
    }

    internal static void AddBffMetrics<T>(T builder) where T : IBffBuilder => builder.Services.AddSingleton<BffMetrics>();


    internal static T AddDynamicFrontends<T>(this T builder)
        where T : IBffServicesBuilder
    {
        builder.Services.AddHybridCache();

        builder.Services.AddHostedService<BffCacheClearingHostedService>();

        builder.Services.AddTransient<IStartupFilter, ConfigureBffStartupFilter>();

        // Register the frontend collection, which will be used to store and retrieve frontends
        builder.Services.AddSingleton<FrontendCollection>();
        // Add a public accessible interface to the frontend collection, so our users can access it
        builder.Services.AddSingleton<IFrontendCollection>((sp) => sp.GetRequiredService<FrontendCollection>());

        builder.Services.AddTransient<CurrentFrontendAccessor>();
        builder.Services.AddSingleton<FrontendSelector>();

        // Add a scheme provider that will inject authentication schemes that are needed for the BFF
        builder.Services.AddTransient<IAuthenticationSchemeProvider, BffAuthenticationSchemeProvider>();

        // Configure the AspNet Core Authentication settings if no 
        // .AddAuthentication().AddCookie().AddOpenIdConnect() was added
        builder.Services.AddSingleton<IPostConfigureOptions<AuthenticationOptions>, BffConfigureAuthenticationOptions>();

        builder.Services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, BffConfigureCookieOptions>();

        builder.Services.AddHttpContextAccessor();

        // Add 'default' configure methods that would have been added by
        // .AddAuthentication().AddCookie().AddOpenIdConnect()
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureCookieAuthenticationOptions>());

        builder.Services.TryAddSingleton<IIndexHtmlClient, IndexHtmlHttpClient>();

        builder.Services.ApplyBackchannelHttpHandlerFromOptions();

        return builder;
    }

    /// <summary>
    /// If the BffOptions.BackchannelHttpHandler is set, this will apply it to the IndexHtmlHttpClient
    /// </summary>
    /// <param name="services"></param>
    internal static void ApplyBackchannelHttpHandlerFromOptions(this IServiceCollection services)
    {
        var indexHtmlClientBuilder = services.AddHttpClient(Constants.HttpClientNames.IndexHtmlHttpClient);

        services.Configure<HttpClientFactoryOptions>(indexHtmlClientBuilder.Name, options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(httpMessageHandlerBuilder =>
            {
                var defaults = httpMessageHandlerBuilder.Services.GetRequiredService<IOptions<BffOptions>>();
                if (defaults.Value.BackchannelHttpHandler != null)
                {
                    httpMessageHandlerBuilder.PrimaryHandler = defaults.Value.BackchannelHttpHandler;
                }
            });
        });
    }

    /// <summary>
    /// Adds a server-side session store using the in-memory store
    /// </summary>
    /// <returns></returns>
    public static T AddServerSideSessions<T>(this T builder) where T : IBffServicesBuilder
    {
        builder.Services.AddServerSideSessionsSupportingServices();
        builder.Services.TryAddSingleton<IUserSessionStore, InMemoryUserSessionStore>();

        //EV: Should this be added again?
        //builder.Services.AddSingleton<IHostedService, SessionCleanupHost>();
        return builder;
    }

    internal static void AddServerSideSessionsSupportingServices(this IServiceCollection services)
    {
        services.AddSingleton<BuildUserSessionPartitionKey>(sp => sp.GetRequiredService<UserSessionPartitionKeyBuilder>().BuildPartitionKey);
        services.AddSingleton<UserSessionPartitionKeyBuilder>();

        services.AddSingleton<UserSessionPartitionKeyBuilder>();
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureApplicationCookieTicketStore>();
        services.AddTransient<IServerTicketStore, ServerSideTicketStore>();
        services.AddTransient<ISessionRevocationService, SessionRevocationService>();
        // only add if not already in DI
    }

    /// <summary>
    /// Adds a server-side session store using the supplied session store implementation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IBffBuilder AddServerSideSessions<T>(this IBffServicesBuilder builder)
        where T : class, IUserSessionStore
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddTransient<IUserSessionStore, T>();
        return builder.AddServerSideSessions();
    }

    public static T AddFrontends<T>(this T builder, params BffFrontend[] frontends)
        where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(frontends);

        // Check for duplicate frontend names
        var duplicateNames = frontends
            .GroupBy(f => f.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate frontend names detected: {string.Join(", ", duplicateNames.Select(n => n))}");
        }

        foreach (var frontend in frontends)
        {
            builder.Services.Add(new ServiceDescriptor(typeof(BffFrontend), frontend));
        }

        return builder;
    }
}
