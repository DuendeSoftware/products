// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
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

            services.AddTransient<DPoPJwtBearerEvents>();
            services.TryAddTransient<IDPoPProofValidator, DPoPProofValidator>();
            services.TryAddTransient<IDPoPNonceValidator, DefaultDPoPNonceValidator>();
            services.AddTransient<DPoPExpirationValidator>();
            services.TryAddTransient<IDPoPProofValidator, DPoPProofValidator>();

            services.AddKeyedHybridCache(
                serviceKey: ServiceProviderKeys.ProofTokenReplayHybridCache,
                opt => opt.DistributedCacheServiceKey = ServiceProviderKeys.ProofTokenReplayDistributedCache);

            services.TryAddTransient<IReplayCache, ReplayCache>();

            services.AddSingleton<ConfigureJwtBearerOptions>();

            services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>>(sp =>
            {
                var distributedCache =
                    sp.GetKeyedService<IDistributedCache>(ServiceProviderKeys.ProofTokenReplayDistributedCache);
                if (distributedCache is null)
                {
                    throw new InvalidOperationException("Replay detection (DPoPOptions.EnableReplayDetection) is enabled, but no IDistributedCache implementation is registered for the key ServiceProviderKeys.ProofTokenReplayDistributedCache. Either disable replay detection or register an IDistributedCache for the key ServiceProviderKeys.ProofTokenReplayDistributedCache. See TODO for more information.");
                }

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
    public const string ProofTokenReplayDistributedCache = nameof(ProofTokenReplayDistributedCache);
}
