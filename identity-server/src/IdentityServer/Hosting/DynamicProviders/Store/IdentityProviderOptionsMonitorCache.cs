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
    private readonly ConcurrentDictionary<(SpaceId Space, string Scheme), Entry> _identityProviders = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly IdentityServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _sweepInterval;
    private long _lastSweepTicks;
    private IReadOnlyList<ISpaceAwareDynamicOptionsCache>? _dynamicOptionsCaches;

    /// <summary>
    /// Test seam: invoked, for each entry sweep has decided is idle, after that decision but
    /// before the eviction CAS runs. Lets a test touch the entry in that window to prove the race
    /// is closed, without timing-dependent sleeps.
    /// </summary>
    internal Action<Entry>? OnEntryDecidedIdle { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityProviderOptionsMonitorCache"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider used to resolve options monitor caches.</param>
    /// <param name="options">The IdentityServer options containing dynamic provider type registrations.</param>
    public IdentityProviderOptionsMonitorCache(IServiceProvider serviceProvider, IdentityServerOptions options)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

        var idleTimeout = options.DynamicProviders.CacheIdleTimeout;
        _sweepInterval = idleTimeout == Timeout.InfiniteTimeSpan
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromTicks(Math.Max(idleTimeout.Ticks / 4, 1));
    }

    /// <summary>
    /// Ensures the options monitor cache is up to date for the given identity provider.
    /// If the provider has changed since it was last observed, the corresponding
    /// <see cref="IOptionsMonitorCache{TOptions}"/> entry is evicted so the authentication
    /// handler will pick up the new configuration. The entry is also evicted the first time a provider is seen
    /// for a scheme in the current space (for example after it was removed), because it cannot be trusted then.
    /// The return value stays <c>false</c> in that case because the provider did not change.
    /// </summary>
    /// <param name="identityProvider">The identity provider to check.</param>
    /// <returns><c>true</c> if the cache entry was evicted because the provider changed; <c>false</c> otherwise.</returns>
    public bool EnsureCacheUpdated(IdentityProvider? identityProvider)
    {
        if (identityProvider == null)
        {
            return false;
        }

        SweepIfDue();

        var key = CurrentSpaceKey(identityProvider.Scheme);

        while (true)
        {
            if (!_identityProviders.TryGetValue(key, out var currentEntry))
            {
                var entry = new Entry(identityProvider, _timeProvider.GetUtcNow());
                if (_identityProviders.TryAdd(key, entry))
                {
                    RemoveCacheEntry(identityProvider);
                    return false;
                }

                continue;
            }

            if (currentEntry.Provider.Equals(identityProvider))
            {
                if (currentEntry.Touch(_timeProvider))
                {
                    return false;
                }

                // Sweep evicted this exact entry between the read above and the touch. An
                // evicted entry counts as "not seen" (the entry is gone from the tracker's point
                // of view), so this is a first observation: install a fresh live entry and evict
                // the options cache, same as the not-found branch above.
                var freshEntry = new Entry(identityProvider, _timeProvider.GetUtcNow());
                if (_identityProviders.TryUpdate(key, freshEntry, currentEntry))
                {
                    RemoveCacheEntry(identityProvider);
                    return false;
                }

                continue;
            }

            var updatedEntry = new Entry(identityProvider, _timeProvider.GetUtcNow());
            if (_identityProviders.TryUpdate(key, updatedEntry, currentEntry))
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

    internal IdentityProvider? GetLatestProvider(string scheme)
    {
        if (!_identityProviders.TryGetValue(CurrentSpaceKey(scheme), out var entry))
        {
            return null;
        }

        // An evicted entry (a concurrent sweep raced this lookup) must not be handed out as live:
        // the caller falls back to treating the provider as unseen.
        return entry.Touch(_timeProvider) ? entry.Provider : null;
    }

    /// <summary>
    /// Test-only, read-only lookup: whether the tracker currently holds an entry for a space and
    /// scheme, so an integration test can assert sweep actually removed/kept it.
    /// </summary>
    internal bool IsTracked(SpaceId space, string scheme) => _identityProviders.ContainsKey((space, scheme));

    /// <summary>
    /// Test-only, read-only lookup of the raw <see cref="Entry"/> for a space and scheme, so a
    /// test can mark it evicted in place (via <see cref="Entry.TryMarkEvicted"/>) without going
    /// through the sweep, to exercise the callers' replacement branches deterministically.
    /// </summary>
    internal Entry? GetEntryForTest(SpaceId space, string scheme) => _identityProviders.TryGetValue((space, scheme), out var entry) ? entry : null;

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

    /// <summary>
    /// Single coordinator for idle-entry reclamation: sweeps this tracker's own entries and, when
    /// due, every registered dynamic provider options cache too. Called from
    /// <see cref="EnsureCacheUpdated"/> (dynamic-scheme traffic) and once per request from
    /// <see cref="Hosting.IdentityServerMiddleware"/> (so reclamation also runs for traffic that
    /// never resolves a dynamic scheme, for example static-only, local-login, or token requests,
    /// or requests that only ever touch a different registered options type).
    /// </summary>
    internal void SweepIdleEntriesIfDue() => SweepIfDue();

    private IReadOnlyList<ISpaceAwareDynamicOptionsCache> DynamicOptionsCaches =>
        _dynamicOptionsCaches ??= _options.DynamicProviders.ProviderTypes.Values
            .Select(providerType => _serviceProvider.GetService(typeof(IOptionsMonitorCache<>).MakeGenericType(providerType.OptionsType)))
            .OfType<ISpaceAwareDynamicOptionsCache>()
            .ToArray();

    private void SweepIfDue()
    {
        var idleTimeout = _options.DynamicProviders.CacheIdleTimeout;
        if (idleTimeout == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var lastSweep = new DateTimeOffset(Interlocked.Read(ref _lastSweepTicks), TimeSpan.Zero);
        if (now - lastSweep < _sweepInterval)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastSweepTicks, now.UtcTicks, lastSweep.UtcTicks) != lastSweep.UtcTicks)
        {
            return;
        }

        SweepEntries(now);

        foreach (var optionsCache in DynamicOptionsCaches)
        {
            optionsCache.SweepIdle(now);
        }
    }

    private void SweepEntries(DateTimeOffset now)
    {
        var idleTimeout = _options.DynamicProviders.CacheIdleTimeout;

        foreach (var pair in _identityProviders)
        {
            var observedTicks = pair.Value.LastUsedTicksRaw;
            if (observedTicks == Entry.Evicted || now - new DateTimeOffset(observedTicks, TimeSpan.Zero) < idleTimeout)
            {
                continue;
            }

            OnEntryDecidedIdle?.Invoke(pair.Value);

            // Mark the entry evicted with a CAS on the same field Touch writes to: if a lookup
            // touched it after we read it as idle, this fails and we leave it alone instead of
            // removing a now-live entry.
            if (pair.Value.TryMarkEvicted(observedTicks))
            {
                ((ICollection<KeyValuePair<(SpaceId Space, string Scheme), Entry>>)_identityProviders).Remove(pair);
            }
        }
    }

    internal sealed class Entry(IdentityProvider provider, DateTimeOffset lastUsed)
    {
        internal const long Evicted = long.MinValue;

        private long _lastUsedTicks = lastUsed.UtcTicks;

        public IdentityProvider Provider { get; } = provider;

        internal long LastUsedTicksRaw => Volatile.Read(ref _lastUsedTicks);

        /// <summary>
        /// Updates the last-used time, unless the entry has already been marked evicted by a
        /// concurrent sweep. Returns <c>false</c> in that case: the caller must not treat this
        /// entry as live.
        /// </summary>
        internal bool Touch(TimeProvider timeProvider)
        {
            while (true)
            {
                var observed = Volatile.Read(ref _lastUsedTicks);
                if (observed == Evicted)
                {
                    return false;
                }

                var now = timeProvider.GetUtcNow().UtcTicks;
                if (Interlocked.CompareExchange(ref _lastUsedTicks, now, observed) == observed)
                {
                    return true;
                }
            }
        }

        internal bool TryMarkEvicted(long observed) => Interlocked.CompareExchange(ref _lastUsedTicks, Evicted, observed) == observed;

        public DateTimeOffset LastUsed => new(Volatile.Read(ref _lastUsedTicks), TimeSpan.Zero);
    }
}
