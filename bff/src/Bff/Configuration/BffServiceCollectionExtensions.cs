// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Duende.Bff.EndpointProcessing;
using Duende.Bff.General;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for the BFF DI services
/// </summary>
public static class BffServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Duende.BFF services to DI
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureAction"></param>
    /// <returns></returns>
    public static BffBuilder AddBff(this IServiceCollection services, Action<BffOptions>? configureAction = null)
    {
        if (configureAction != null)
        {
            services.Configure(configureAction);
        }

        services.AddDistributedMemoryCache();
        services.AddOpenIdConnectAccessTokenManagement();
        services.AddSingleton<IConfigureOptions<UserTokenManagementOptions>, ConfigureUserTokenManagementOptions>();

        services.AddTransient<IReturnUrlValidator, LocalUrlReturnUrlValidator>();
#pragma warning disable CS0618 // Type or member is obsolete
        services.TryAddSingleton<DefaultAccessTokenRetriever>();

        // management endpoints
        services.AddTransient<ILoginService, DefaultLoginService>();
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddTransient<ISilentLoginService, DefaultSilentLoginService>();
#pragma warning restore CS0618 // Type or member is obsolete
        services.AddTransient<ISilentLoginCallbackService, DefaultSilentLoginCallbackService>();
        services.AddTransient<ILogoutService, DefaultLogoutService>();
        services.AddTransient<IUserService, DefaultUserService>();
        services.AddTransient<IBackchannelLogoutService, DefaultBackchannelLogoutService>();
        services.AddTransient<IDiagnosticsService, DefaultDiagnosticsService>();

        // session management
        services.TryAddTransient<ISessionRevocationService, NopSessionRevocationService>();

        // cookie configuration
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureSlidingExpirationCheck>();
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureApplicationCookieRevokeRefreshToken>();

        services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, PostConfigureOidcOptionsForSilentLogin>();

        // wrap ASP.NET Core
        services.AddAuthentication();
        services.AddTransientDecorator<IAuthenticationService, BffAuthenticationService>();

#pragma warning restore CS0618 // Type or member is obsolete

        return new BffBuilder(services);
    }
}
