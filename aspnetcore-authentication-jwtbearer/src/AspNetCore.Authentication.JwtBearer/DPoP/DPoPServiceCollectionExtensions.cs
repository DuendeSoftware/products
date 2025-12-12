// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Extension methods for setting up DPoP on a JwtBearer authentication scheme.
/// </summary>
public static class DPoPServiceCollectionExtensions
{
    /// <summary>
    /// Sets up DPoP on a JwtBearer authentication scheme.
    /// </summary>
    public static IServiceCollection ConfigureDPoPTokensForScheme(this IServiceCollection services, string scheme)
    {
        services.AddOptions<DPoPOptions>();

        services.AddTransient<DPoPJwtBearerEvents>();
        services.TryAddTransient<IDPoPProofValidator, DPoPProofValidator>();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        services.TryAddTransient<IReplayCache, ReplayCache>();

        services.AddSingleton<ConfigureJwtBearerOptions>();
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>>(sp =>
        {
            var opt = sp.GetRequiredService<ConfigureJwtBearerOptions>();
            opt.Scheme = scheme;
            return opt;
        });
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        return services;
    }

    /// <summary>
    /// Sets up DPoP on a JwtBearer authentication scheme, and configures <see cref="DPoPOptions"/>.
    /// </summary>
    public static IServiceCollection ConfigureDPoPTokensForScheme(this IServiceCollection services, string scheme, Action<DPoPOptions> configure)
    {
        services.Configure(scheme, configure);
        return services.ConfigureDPoPTokensForScheme(scheme);
    }
}
