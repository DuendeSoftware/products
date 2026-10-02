// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Concurrent;
using Duende.IdentityServer.Models;
using Duende.Spaces;

namespace Duende.IdentityServer.Hosting.DynamicProviders;
// this is designed as a per-request cache is to ensure that a scheme loaded from the cache is still available later in the
// request and made available anywhere else during this request (in case the static cache times out across 
// 2 calls within the same request)
// also, we need a non-async version that can be used from within the non-async Configure API in IConfigureNamedOptions<>

/// <summary>
/// Cache for DynamicAuthenticationScheme.
/// </summary>
/// <remarks>
/// Entries are partitioned by the current space, so options built after a space switch use the
/// new space's provider. This does not isolate a handler already created earlier in the same
/// request: ASP.NET Core's per-request handler provider reuses it by scheme name, with the options
/// it was created with.
/// </remarks>
public class DynamicAuthenticationSchemeCache
{
    private readonly ConcurrentDictionary<(SpaceId Space, string Scheme), DynamicAuthenticationScheme> _cache = new();
    private readonly ISpaceContextAccessor _spaceContextAccessor;

    /// <summary>
    /// Ctor
    /// </summary>
    public DynamicAuthenticationSchemeCache()
        : this(null)
    {
    }

    internal DynamicAuthenticationSchemeCache(ISpaceContextAccessor spaceContextAccessor) => _spaceContextAccessor = spaceContextAccessor;

    /// <summary>
    /// Adds the scheme.
    /// </summary>
    public void Add(string name, DynamicAuthenticationScheme item) => _cache.TryAdd(Key(name), item);

    /// <summary>
    /// Gets the scheme.
    /// </summary>
    public DynamicAuthenticationScheme Get(string name)
    {
        _cache.TryGetValue(Key(name), out var item);
        return item;
    }

    /// <summary>
    /// Returns the downcast IdentityProvider.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <returns></returns>
    public T GetIdentityProvider<T>(string name)
        where T : IdentityProvider => Get(name)?.IdentityProvider as T;

    private (SpaceId Space, string Scheme) Key(string name) =>
        (_spaceContextAccessor?.GetSpaceIdOrDefault() ?? SpaceId.Default, name ?? string.Empty);
}
