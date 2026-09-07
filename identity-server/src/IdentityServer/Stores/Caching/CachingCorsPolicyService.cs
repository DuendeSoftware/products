// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.Default;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Caching decorator for ICorsPolicyService
/// </summary>
/// <seealso cref="IdentityServer.Services.ICorsPolicyService" />
public class CachingCorsPolicyService<T> : ICorsPolicyService
    where T : ICorsPolicyService
{
    private readonly CachePolicy<CorsCacheEntry> _policy;
    private readonly IdentityServerOptions _options;
    private readonly HybridCache _cache;
    private readonly ICorsPolicyService _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingCorsPolicyService{T}"/> class.
    /// </summary>
    /// <param name="policy">The cache policy.</param>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="cache">The cache.</param>
    public CachingCorsPolicyService(
        CachePolicy<CorsCacheEntry> policy,
        IdentityServerOptions options,
        T inner,
        [FromKeyedServices(ServiceProviderKeys.ConfigurationStoreCache)] HybridCache cache)
    {
        _policy = policy;
        _options = options;
        _inner = inner;
        _cache = cache;
    }

    /// <inheritdoc/>
    public virtual async Task<bool> IsOriginAllowedAsync(string origin, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingCorsPolicyService.IsOriginAllowed");
        activity?.SetTag(Tracing.Properties.Origin, origin);

        var entry = await _cache.GetOrCreateAsync(
            _policy.BuildKey(origin),
            (inner: _inner, origin),
            static async (state, cancel) => new CorsCacheEntry(await state.inner.IsOriginAllowedAsync(state.origin, cancel)),
            _policy.WriteOptions(_options.Caching.CorsExpiration),
            tags: _policy.Tags,
            cancellationToken: ct);

        return entry?.Allowed ?? false;
    }
}
