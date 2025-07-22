// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using Duende.Bff.Configuration;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class FrontendCollection : IDisposable, IFrontendCollection
{
    private readonly IBffPluginLoader[] _plugins;
    private readonly object _syncRoot = new();

    /// <summary>
    /// Backing store for the frontends. This is marked 'volatile' because it can be read / updated from multiple threads.
    /// When adding / updating, we create a new array to avoid locking the entire list for read operations.
    /// </summary>
    private volatile BffFrontend[] _frontends;

    private readonly IDisposable? _stopSubscription;

    internal event Action<BffFrontend> OnFrontendChanged = (_) => { };
    internal event Action<BffFrontend> OnFrontendAdded = (_) => { };

    public FrontendCollection(
        IOptionsMonitor<BffConfiguration> bffConfiguration,
        IEnumerable<IBffPluginLoader> plugins,
        IEnumerable<BffFrontend>? frontendsConfiguredDuringStartup = null
    )
    {
        _plugins = plugins.ToArray();
        _frontends = ReadFrontends(bffConfiguration.CurrentValue, frontendsConfiguredDuringStartup ?? []);

        // Subscribe to configuration changes
        _stopSubscription = bffConfiguration.OnChange(config =>
        {
            BffFrontend[] removedFrontends;
            BffFrontend[] addedFrontends;
            BffFrontend[] changedFrontends;

            lock (_syncRoot)
            {
                var newFrontends = ReadFrontends(config, frontendsConfiguredDuringStartup ?? []);

                var oldFrontends = _frontends.ToArray();

                removedFrontends = oldFrontends
                    .Where(frontend => newFrontends.All(x => x.Name != frontend.Name))
                    .ToArray();

                changedFrontends = newFrontends
                    .Where(frontend => oldFrontends.Any(x => x.Name == frontend.Name && IsUpdated(x, frontend)))
                    .ToArray();

                addedFrontends = newFrontends
                    .Where(frontend => oldFrontends.All(x => x.Name != frontend.Name))
                    .ToArray();

                Interlocked.Exchange(ref _frontends, newFrontends);
            }

            foreach (var added in addedFrontends)
            {
                OnFrontendAdded(added);
            }

            foreach (var changed in changedFrontends)
            {
                OnFrontendChanged(changed);
            }

            foreach (var removed in removedFrontends)
            {
                OnFrontendChanged(removed);
            }
        });
    }

    private static bool IsUpdated(BffFrontend left, BffFrontend right)
    {
        if (!left.Equals(right))
        {
            return true;
        }

        // We can't compare the Action delegates. This means that, if there are options,
        // then we assume they have changed. This is not as efficient as it could be,
        // but at least the caches get cleared. Should this cause perf issues, we can
        // actually execute the configure options and compare the results.
        if (left.ConfigureCookieOptions != null
            || right.ConfigureCookieOptions != null
            || left.ConfigureOpenIdConnectOptions != null
            || right.ConfigureOpenIdConnectOptions != null)
        {
            return true;
        }

        return false;
    }

    private BffFrontend[] ReadFrontends(
        BffConfiguration bffConfiguration,
        IEnumerable<BffFrontend> inMemory)
    {
        var fromOptions = bffConfiguration.Frontends.Select(x =>
        {
            var frontendConfiguration = x.Value;

            var frontendName = BffFrontendName.Parse(x.Key);
            var extensions = _plugins.Select(p => p.LoadExtension(frontendName)).OfType<IBffPlugin>().ToArray();

            var frontend = new BffFrontend
            {
                Name = frontendName,
                IndexHtmlUrl = frontendConfiguration.IndexHtmlUrl,

                ConfigureOpenIdConnectOptions = frontendConfiguration.Oidc == null
                    ? null
                    : opt =>
                    {
                        frontendConfiguration.Oidc?.ApplyTo(opt);
                    },
                ConfigureCookieOptions = frontendConfiguration.Cookies == null
                    ? null
                    : opt =>
                    {
                        frontendConfiguration.Cookies?.ApplyTo(opt);
                    },
                SelectionCriteria = new FrontendSelectionCriteria()
                {
                    MatchingOrigin = Origin.ParseOrDefault(frontendConfiguration.MatchingOrigin),
                    MatchingPath = string.IsNullOrEmpty(frontendConfiguration.MatchingPath) ? null : frontendConfiguration.MatchingPath,
                },
                DataExtensions = extensions
            };
            return frontend;
        });

        return inMemory.Concat(fromOptions).ToArray();
    }

    public void AddOrUpdate(BffFrontend frontend)
    {
        var existingUpdated = false;
        // Lock to avoid dirty writes from multiple threads. 
        lock (_syncRoot)
        {
            var existing = _frontends.FirstOrDefault(x => x.Name == frontend.Name);

            if (existing != null)
            {
                existingUpdated = true;
            }

            if (existing != null && !IsUpdated(frontend, existing))
            {
                return;
            }

            // By replacing the array, we avoid locking the entire list for read operations.
            Interlocked.Exchange(ref _frontends, _frontends
                .Where(x => x.Name != frontend.Name)
                .Append(frontend)
                .ToArray());
        }

        if (existingUpdated)
        {
            OnFrontendChanged(frontend);
        }
        else
        {
            OnFrontendAdded(frontend);
        }

    }

    public void Remove(BffFrontendName frontendName)
    {
        BffFrontend? existing;

        lock (_syncRoot)
        {
            existing = _frontends.FirstOrDefault(x => x.Name == frontendName);
            if (existing == null)
            {
                return;
            }

            // By replacing the array, we avoid locking the entire list for read operations.
            Interlocked.Exchange(ref _frontends, _frontends
                .Where(x => x.Name != frontendName)
                .ToArray());
        }

        OnFrontendChanged(existing);
    }

    public int Count => _frontends.Length;

    void IDisposable.Dispose() => _stopSubscription?.Dispose();
    public IEnumerator<BffFrontend> GetEnumerator() => ((IEnumerable<BffFrontend>)_frontends).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _frontends.GetEnumerator();
}
