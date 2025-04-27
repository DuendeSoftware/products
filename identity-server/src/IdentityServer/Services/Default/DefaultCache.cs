// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// IMemoryCache-based implementation of the cache
/// </summary>
/// <typeparam name="T"></typeparam>
/// <seealso cref="ICache{T}" />
public class DefaultCache<T> : ICache<T>
    where T : class
{
    private const string KeySeparator = "-";

    /// <summary>
    /// The IdentityServerOptions.
    /// </summary>
    public IdentityServerOptions IdentityServerOptions { get; }

    /// <summary>
    /// The memory cache.
    /// </summary>
    protected IMemoryCache Cache { get; }

    /// <summary>
    /// A lock used for concurrency.
    /// </summary>
    protected IConcurrencyLock<DefaultCache<T>> ConcurrencyLock { get; }

    /// <summary>
    /// The logger.
    /// </summary>
    protected ILogger<DefaultCache<T>> Logger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCache{T}"/> class.
    /// </summary>
    /// <param name="identityServerOptions"></param>
    /// <param name="cache">The cache.</param>
    /// <param name="concurrencyLock"></param>
    /// <param name="logger">The logger.</param>
    public DefaultCache(IdentityServerOptions identityServerOptions, IMemoryCache cache, IConcurrencyLock<DefaultCache<T>> concurrencyLock, ILogger<DefaultCache<T>> logger)
    {
        IdentityServerOptions = identityServerOptions;
        Cache = cache;
        ConcurrencyLock = concurrencyLock;
        Logger = logger;
    }

    /// <summary>
    /// Used to create the key for the cache based on the data type being cached.
    /// </summary>
    /// <param name="key"></param>
    protected string GetKey(string key) => typeof(T).FullName + KeySeparator + key;

    /// <inheritdoc/>
    public Task<T> GetAsync(string key)
    {
        using var activity = Tracing.CacheActivitySource.StartActivity("DefaultCache.Get");

        key = GetKey(key);
        var item = Cache.Get<T>(key);
        return Task.FromResult(item);
    }

    /// <inheritdoc/>
    public Task SetAsync(string key, T item, TimeSpan expiration)
    {
        using var activity = Tracing.CacheActivitySource.StartActivity("DefaultCache.Set");

        key = GetKey(key);
        Cache.Set(key, item, expiration);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key)
    {
        using var activity = Tracing.CacheActivitySource.StartActivity("DefaultCache.Remove");

        key = GetKey(key);
        Cache.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<T> GetOrAddAsync(string key, TimeSpan duration, Func<Task<T>> get)
    {
        using var activity = Tracing.CacheActivitySource.StartActivity("DefaultCache.GetOrAdd");

        ArgumentNullException.ThrowIfNull(get);
        if (key == null) return null;

        var item = await GetAsync(key);

        if (item == null)
        {
            if (false == await ConcurrencyLock.LockAsync((int)IdentityServerOptions.Caching.CacheLockTimeout.TotalMilliseconds))
            {
                throw new Exception($"Failed to obtain cache lock for: '{GetType()}'");
            }

            try
            {
                // double check
                item = await GetAsync(key);

                if (item == null)
                {
                    Logger.LogTrace("Cache miss for {cacheKey}", key);

                    item = await get();

                    if (item != null)
                    {
                        Logger.LogTrace("Setting item in cache for {cacheKey}", key);
                        await SetAsync(key, item, duration);
                    }
                }
                else
                {
                    Logger.LogTrace("Cache hit for {cacheKey}", key);
                }
            }
            finally
            {
                ConcurrencyLock.Unlock();
            }
        }
        else
        {
            Logger.LogTrace("Cache hit for {cacheKey}", key);
        }

        return item;
    }
}
