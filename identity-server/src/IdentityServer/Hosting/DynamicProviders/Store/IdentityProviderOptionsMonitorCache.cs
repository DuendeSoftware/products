// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Tracks previously observed <see cref="IdentityProvider"/> instances per scheme and
/// evicts the corresponding ASP.NET Core <see cref="IOptionsMonitorCache{TOptions}"/>
/// entry when a provider's configuration has changed. This allows the authentication
/// handler options to stay in sync with the identity provider store without requiring
/// an HTTP context for service resolution.
/// </summary>
public sealed class IdentityProviderOptionsMonitorCache
{
    private readonly ConcurrentDictionary<string, IdentityProvider> _identityProviders = new(StringComparer.Ordinal);
    private readonly IServiceProvider _serviceProvider;
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityProviderOptionsMonitorCache"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider used to resolve options monitor caches.</param>
    /// <param name="options">The IdentityServer options containing dynamic provider type registrations.</param>
    public IdentityProviderOptionsMonitorCache(IServiceProvider serviceProvider, IdentityServerOptions options)
    {
        _serviceProvider = serviceProvider;
        _options = options;
    }

    /// <summary>
    /// Ensures the options monitor cache is up to date for the given identity provider.
    /// If the provider has changed since it was last observed, the corresponding
    /// <see cref="IOptionsMonitorCache{TOptions}"/> entry is evicted so the authentication
    /// handler will pick up the new configuration.
    /// </summary>
    /// <param name="identityProvider">The identity provider to check.</param>
    /// <returns><c>true</c> if the cache entry was evicted because the provider changed; <c>false</c> otherwise.</returns>
    public bool EnsureCacheUpdated(IdentityProvider? identityProvider)
    {
        if (identityProvider == null)
        {
            return false;
        }

        while (true)
        {
            if (!_identityProviders.TryGetValue(identityProvider.Scheme, out var currentValue))
            {
                if (_identityProviders.TryAdd(identityProvider.Scheme, identityProvider))
                {
                    return false;
                }

                continue;
            }

            if (currentValue.Equals(identityProvider))
            {
                return false;
            }

            if (_identityProviders.TryUpdate(identityProvider.Scheme, identityProvider, currentValue))
            {
                RemoveCacheEntry(identityProvider);
                return true;
            }
        }
    }

    private void RemoveCacheEntry(IdentityProvider identityProvider)
    {
        var provider = _options.DynamicProviders.FindProviderType(identityProvider.Type);
        if (provider == null)
        {
            return;
        }

        var optionsMonitorType = typeof(IOptionsMonitorCache<>).MakeGenericType(provider.OptionsType);
        var optionsCache = _serviceProvider.GetService(optionsMonitorType);
        var tryRemove = optionsMonitorType.GetMethod(nameof(IOptionsMonitorCache<object>.TryRemove));

        if (optionsCache != null && tryRemove != null)
        {
            tryRemove.Invoke(optionsCache, [identityProvider.Scheme]);
        }
    }
}
