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
    // TODO does need to be volatile?
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
            BffFrontend[] changedFrontends;

            lock (_syncRoot)
            {
                var newFrontends = ReadFrontends(config, frontendsConfiguredDuringStartup ?? []);

                var oldFrontends = _frontends.ToArray();

                removedFrontends = oldFrontends
                    .Where(frontend => newFrontends.All(x => x.Name != frontend.Name))
                    .ToArray();

                changedFrontends = newFrontends
                    .Where(frontend => oldFrontends.Any(x => x.Name == frontend.Name))
                    .ToArray();

                Interlocked.Exchange(ref _frontends, newFrontends);
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

    private BffFrontend[] ReadFrontends(
        BffConfiguration bffConfiguration,
        IEnumerable<BffFrontend> inMemory)
    {
        var fromOptions = bffConfiguration.Frontends.Select(x =>
        {
            var frontend = x.Value;

            var frontendName = BffFrontendName.Parse(x.Key);
            var extensions = _plugins.Select(p => p.LoadExtension(frontendName)).OfType<IBffPlugin>().ToArray();

            return new BffFrontend
            {
                Name = frontendName,
                IndexHtmlUrl = frontend.IndexHtmlUrl,
                ConfigureOpenIdConnectOptions = opt =>
                {
                    frontend.Oidc?.ApplyTo(opt);
                },
                ConfigureCookieOptions = opt =>
                {
                    frontend.Cookies?.ApplyTo(opt);
                },
                SelectionCriteria = new FrontendSelectionCriteria()
                {
                    // todo: parse or default
                    MatchingOrigin = string.IsNullOrEmpty(frontend.MatchingOrigin)
                        ? null
                        : Origin.Parse(frontend.MatchingOrigin),
                    MatchingPath = string.IsNullOrEmpty(frontend.MatchingPath) ? null : frontend.MatchingPath,
                },
                DataExtensions = extensions
            };
        });

        return inMemory.Concat(fromOptions).ToArray();
    }

    //private static RemoteApi MapFrom(RemoteApiConfig config)
    //{
    //    Type? type = null;

    //    if (config.TokenRetrieverTypeName != null)
    //    {
    //        type = Type.GetType(config.TokenRetrieverTypeName);
    //        if (type == null)
    //        {
    //            throw new InvalidOperationException($"Type {config.TokenRetrieverTypeName} not found.");
    //        }
    //        if (!typeof(IAccessTokenRetriever).IsAssignableFrom(type))
    //        {
    //            throw new InvalidOperationException($"Type {config.TokenRetrieverTypeName} must implement IAccessTokenRetriever.");
    //        }
    //    }

    //    var api = new RemoteApi
    //    {
    //        LocalPath = config.LocalPath ?? throw new InvalidOperationException("localpath cannot be empty"),
    //        TargetUri = config.TargetUri ?? throw new InvalidOperationException("targeturi cannot be empty"),
    //        RequiredTokenType = config.RequiredTokenType,
    //        AccessTokenRetrieverType = type,
    //        Parameters = Map(config.UserAccessTokenParameters)
    //    };

    //    return api;
    //}

    //private static BffUserAccessTokenParameters? Map(UserAccessTokenParameters? config)
    //{
    //    if (config == null)
    //    {
    //        return null;
    //    }

    //    return new BffUserAccessTokenParameters
    //    {
    //        SignInScheme = config.SignInScheme,
    //        ChallengeScheme = config.ChallengeScheme,
    //        ForceRenewal = config.ForceRenewal,
    //        Resource = config.Resource
    //    };
    //}

    public void AddOrUpdate(BffFrontend frontend)
    {
        // Lock to avoid dirty writes from multiple threads. 
        lock (_syncRoot)
        {
            // By replacing the array, we avoid locking the entire list for read operations.
            Interlocked.Exchange(ref _frontends, _frontends
                .Where(x => x.Name != frontend.Name)
                .Append(frontend)
                .ToArray());
        }

        OnFrontendChanged(frontend);
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
