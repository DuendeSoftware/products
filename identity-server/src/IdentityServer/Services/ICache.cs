// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Services;

/// <summary>
/// Abstract interface to model data caching
/// </summary>
/// <typeparam name="T">The data type to be cached</typeparam>
public interface ICache<T>
    where T : class
{
    /// <summary>
    /// Gets the cached data based upon a key index.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The cached item, or <c>null</c> if no item matches the key.</returns>
    Task<T?> GetAsync(string key);

    /// <summary>
    /// Gets the cached data based upon a key index.
    /// If the item is not found, the <c>get</c> function is used to obtain the item and populate the cache.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="duration">The duration.</param>
    /// <param name="get">The function to obtain the item.</param>
    /// <returns>The cached item.</returns>
    Task<T> GetOrAddAsync(string key, TimeSpan duration, Func<Task<T>> get);

    /// <summary>
    /// Caches the data based upon a key
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="item">The item.</param>
    /// <param name="expiration">The expiration.</param>
    Task SetAsync(string key, T item, TimeSpan expiration);

    /// <summary>
    /// Removes the cached data based upon a key index.
    /// </summary>
    /// <param name="key">The key.</param>
    Task RemoveAsync(string key);
}
