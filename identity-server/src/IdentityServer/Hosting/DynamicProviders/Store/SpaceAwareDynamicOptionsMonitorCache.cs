// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.Spaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting.DynamicProviders.Store;

/// <summary>
/// Dynamic-scheme options are held in a separate backing store keyed by <c>(SpaceId, name)</c>,
/// so a bare/static scheme cannot alias them. When no space is configured, the
/// <see cref="SpaceId.Default"/> space is used.
/// </summary>
/// <remarks>
/// A name is routed to the dynamic store only once the scheme has been resolved as dynamic in the
/// current request. <see cref="TryRemove"/> outside a dynamic request context evicts the bare name
/// from every space (so framework options reloads via <see cref="IOptionsMonitor{TOptions}"/>
/// work), as well as the unqualified store. A direct <see cref="IOptionsMonitor{TOptions}"/> read
/// before the scheme is resolved in the request still returns unconfigured options from the
/// unqualified store rather than the space's options.
/// </remarks>
/// <typeparam name="TOptions">The options type.</typeparam>
internal sealed class SpaceAwareDynamicOptionsMonitorCache<TOptions>(
    ISpaceContextAccessor? accessor,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    TimeSpan idleTimeout) : IOptionsMonitorCache<TOptions>, ISpaceAwareDynamicOptionsCache
    where TOptions : class
{
    private readonly OptionsCache<TOptions> _unqualified = new();
    private readonly ConcurrentDictionary<(SpaceId Space, string Name), Entry> _dynamic = new();
    private readonly TimeSpan _sweepInterval = idleTimeout == Timeout.InfiniteTimeSpan
        ? Timeout.InfiniteTimeSpan
        : TimeSpan.FromTicks(Math.Max(idleTimeout.Ticks / 4, 1));
    private long _lastSweepTicks;

    /// <summary>
    /// Test seam: invoked, for each entry sweep has decided is idle, after that decision but
    /// before the eviction CAS runs. Lets a test touch the entry in that window to prove the race
    /// is closed, without timing-dependent sleeps.
    /// </summary>
    internal Action<Entry>? OnEntryDecidedIdle { get; set; }

    public void Clear()
    {
        _unqualified.Clear();
        _dynamic.Clear();
    }

    public TOptions GetOrAdd(string? name, Func<TOptions> createOptions)
    {
        name ??= Options.DefaultName;
        if (IsDynamic(name))
        {
            SweepIfDue();
            var key = CurrentSpaceKey(name);

            while (true)
            {
                var entry = _dynamic.GetOrAdd(key, _ => new Entry(new Lazy<TOptions>(createOptions, LazyThreadSafetyMode.ExecutionAndPublication), timeProvider.GetUtcNow()));
                if (entry.Touch(timeProvider))
                {
                    return entry.Value.Value;
                }

                // Sweep evicted this exact entry concurrently (between GetOrAdd returning it and
                // Touch observing the eviction marker). Replace it with a fresh, live entry.
                var freshEntry = new Entry(new Lazy<TOptions>(createOptions, LazyThreadSafetyMode.ExecutionAndPublication), timeProvider.GetUtcNow());
                if (_dynamic.TryUpdate(key, freshEntry, entry))
                {
                    return freshEntry.Value.Value;
                }
            }
        }

        return _unqualified.GetOrAdd(name, createOptions);
    }

    public bool TryAdd(string? name, TOptions options)
    {
        name ??= Options.DefaultName;
        if (IsDynamic(name))
        {
            SweepIfDue();
            return _dynamic.TryAdd(CurrentSpaceKey(name), new Entry(new Lazy<TOptions>(options), timeProvider.GetUtcNow()));
        }

        return _unqualified.TryAdd(name, options);
    }

    public bool TryRemove(string? name)
    {
        name ??= Options.DefaultName;
        if (IsDynamic(name))
        {
            return _dynamic.TryRemove(CurrentSpaceKey(name), out _);
        }

        var removedUnqualified = _unqualified.TryRemove(name);
        var removedAnyDynamic = false;
        foreach (var key in _dynamic.Keys)
        {
            if (key.Name == name && _dynamic.TryRemove(key, out _))
            {
                removedAnyDynamic = true;
            }
        }

        return removedUnqualified || removedAnyDynamic;
    }

    public bool TryRemoveDynamic(string name) => _dynamic.TryRemove(CurrentSpaceKey(name), out _);

    /// <summary>
    /// Unthrottled sweep entry point used by <see cref="IdentityProviderOptionsMonitorCache"/>,
    /// the single sweep coordinator: it decides when a sweep is due (reclamation must also run
    /// for traffic that never touches this particular options cache), and this cache only
    /// contributes the actual eviction logic. Also updates this cache's own last-sweep
    /// timestamp, so its opportunistic <see cref="SweepIfDue"/> (triggered from <see cref="GetOrAdd"/>)
    /// does not redundantly repeat the walk immediately afterward.
    /// </summary>
    public void SweepIdle(DateTimeOffset now)
    {
        if (idleTimeout == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        SweepEntries(now);
        Interlocked.Exchange(ref _lastSweepTicks, now.UtcTicks);
    }

    /// <summary>
    /// Test-only, read-only lookup bypassing the normal request-scoped dynamic-scheme gating, so
    /// an integration test can assert sweep actually removed/kept an entry for a given space.
    /// </summary>
    internal bool ContainsDynamic(SpaceId space, string name) => _dynamic.ContainsKey((space, name));

    /// <summary>
    /// Test-only, read-only lookup of the raw <see cref="Entry"/> for a space and name, so a test
    /// can mark it evicted in place (via <see cref="Entry.TryMarkEvicted"/>) without going through
    /// the sweep, to exercise the callers' replacement branches deterministically.
    /// </summary>
    internal Entry? GetEntryForTest(SpaceId space, string name) => _dynamic.TryGetValue((space, name), out var entry) ? entry : null;

    private (SpaceId Space, string Name) CurrentSpaceKey(string name) =>
        (accessor?.GetSpaceIdOrDefault() ?? SpaceId.Default, name);

    private bool IsDynamic(string name) =>
        httpContextAccessor.HttpContext?.RequestServices.GetService<DynamicAuthenticationSchemeCache>()?.Get(name) is not null;

    private void SweepIfDue()
    {
        if (idleTimeout == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
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
    }

    private void SweepEntries(DateTimeOffset now)
    {
        foreach (var pair in _dynamic)
        {
            var observedTicks = pair.Value.LastUsedTicksRaw;
            if (observedTicks == Entry.Evicted || now - new DateTimeOffset(observedTicks, TimeSpan.Zero) < idleTimeout)
            {
                continue;
            }

            OnEntryDecidedIdle?.Invoke(pair.Value);

            // Mark the entry evicted with a CAS on the same field Touch writes to: if a request
            // touched it after we read it as idle, this fails and we leave it alone instead of
            // removing a now-live entry.
            if (pair.Value.TryMarkEvicted(observedTicks))
            {
                ((ICollection<KeyValuePair<(SpaceId Space, string Name), Entry>>)_dynamic).Remove(pair);
            }
        }
    }

    internal sealed class Entry(Lazy<TOptions> value, DateTimeOffset lastUsed)
    {
        internal const long Evicted = long.MinValue;

        private long _lastUsedTicks = lastUsed.UtcTicks;

        public Lazy<TOptions> Value { get; } = value;

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
