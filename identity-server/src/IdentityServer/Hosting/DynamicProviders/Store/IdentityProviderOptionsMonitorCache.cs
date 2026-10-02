// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders.Store;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly ConcurrentDictionary<(SpaceId Space, string Scheme), IdentityProvider> _identityProviders = new();
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

        var key = CurrentSpaceKey(identityProvider.Scheme);

        while (true)
        {
            if (!_identityProviders.TryGetValue(key, out var currentValue))
            {
                if (_identityProviders.TryAdd(key, identityProvider))
                {
                    return false;
                }

                continue;
            }

            if (currentValue.Equals(identityProvider))
            {
                return false;
            }

            if (_identityProviders.TryUpdate(key, identityProvider, currentValue))
            {
                RemoveCacheEntry(identityProvider);
                return true;
            }
        }
    }

    /// <summary>
    /// Evicts the current space's tracker and options entries for a deleted or disabled provider.
    /// Probes every registered provider type since the provider's type is unknown.
    /// </summary>
    internal void Remove(string scheme)
    {
        _identityProviders.TryRemove(CurrentSpaceKey(scheme), out _);

        foreach (var providerType in _options.DynamicProviders.ProviderTypes.Values)
        {
            RemoveDynamicOptionsEntry(providerType.OptionsType, scheme);
        }
    }

    private (SpaceId Space, string Scheme) CurrentSpaceKey(string scheme) =>
        (_serviceProvider.GetService<ISpaceContextAccessor>()?.GetSpaceIdOrDefault() ?? SpaceId.Default, scheme);

    private void RemoveCacheEntry(IdentityProvider identityProvider)
    {
        var provider = _options.DynamicProviders.FindProviderType(identityProvider.Type);
        if (provider == null)
        {
            return;
        }

        RemoveDynamicOptionsEntry(provider.OptionsType, identityProvider.Scheme);
    }

    private void RemoveDynamicOptionsEntry(Type optionsType, string scheme)
    {
        var optionsMonitorType = typeof(IOptionsMonitorCache<>).MakeGenericType(optionsType);
        var optionsCache = _serviceProvider.GetService(optionsMonitorType);

        if (optionsCache is ISpaceAwareDynamicOptionsCache spaceAware)
        {
            spaceAware.TryRemoveDynamic(scheme);
            return;
        }

        var tryRemove = optionsMonitorType.GetMethod(nameof(IOptionsMonitorCache<object>.TryRemove));
        if (optionsCache != null && tryRemove != null)
        {
            tryRemove.Invoke(optionsCache, [scheme]);
        }
    }
}
