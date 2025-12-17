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
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Sets up DPoP on a JwtBearer authentication scheme.
        /// </summary>
        public IServiceCollection ConfigureDPoPTokensForScheme(string scheme)
        {
            services.AddOptions<DPoPOptions>();

            services.AddSingleton<DPoPJwtBearerEvents>();
            services.TryAddTransient<IDPoPProofValidator, DPoPProofValidator>();
            services.TryAddTransient<IDPoPNonceValidator, DefaultDPoPNonceValidator>();
            services.AddTransient<DPoPExpirationValidator>();
            services.TryAddTransient<IDPoPProofValidator, DPoPProofValidator>();

            services.TryAddTransient<DPoPHybridCacheProvider>();
            services.TryAddTransient<IReplayCache, ReplayCache>();

            services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>>(sp =>
            {
                var events = sp.GetRequiredService<DPoPJwtBearerEvents>();
                return new ConfigureJwtBearerOptions(events)
                {
                    Scheme = scheme
                };
            });

            return services;
        }

        /// <summary>
        /// Sets up DPoP on a JwtBearer authentication scheme, and configures <see cref="DPoPOptions"/>.
        /// </summary>
        public IServiceCollection ConfigureDPoPTokensForScheme(string scheme, Action<DPoPOptions> configure)
        {
            services.Configure(scheme, configure);
            return services.ConfigureDPoPTokensForScheme(scheme);
        }
    }
}

/// <summary>
/// Keys that are used to inject different implementations into a specific service.
/// </summary>
public static class ServiceProviderKeys
{
    public const string ProofTokenReplayHybridCache = nameof(ProofTokenReplayHybridCache);
}
