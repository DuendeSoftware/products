// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Duende.AccessTokenManagement.OpenIdConnect;

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
        services.TryAddSingleton<DefaultAccessTokenRetriever>();

        // management endpoints
        services.AddTransient<ILoginService, DefaultLoginService>();
        services.AddTransient<ISilentLoginService, DefaultSilentLoginService>();
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

        return new BffBuilder(services);
    }
}
