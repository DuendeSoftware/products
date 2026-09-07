// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Shared state for InMemoryKeyStoreCache. This is a singleton that holds the cache entries for all partitions.
/// </summary>
internal sealed class InMemoryKeyStoreCacheState
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    // Immutable class (not a record) so that equality is reference identity. This is required so that the
    // conditional removal below (via ICollection<KeyValuePair<>>.Remove) only removes the exact entry that was
    // observed, and not a different entry that may have replaced it concurrently.
    private sealed class CacheEntry(IReadOnlyCollection<KeyContainer> keys, DateTime expires)
    {
        public IReadOnlyCollection<KeyContainer> Keys { get; } = keys;
        public DateTime Expires { get; } = expires;
    }

    /// <summary>
    /// Gets cached keys for the specified partition key if they exist and are not expired.
    /// </summary>
    internal IReadOnlyCollection<KeyContainer>? GetKeys(string partitionKey, DateTime now)
    {
        if (!_entries.TryGetValue(partitionKey, out var entry))
        {
            return null;
        }

        if (entry.Expires >= now)
        {
            return entry.Keys;
        }

        RemoveExact(partitionKey, entry);
        return null;
    }

    /// <summary>
    /// Stores keys for the specified partition key with an expiration time.
    /// </summary>
    internal void StoreKeys(string partitionKey, IReadOnlyCollection<KeyContainer> keys, DateTime now, DateTime expires)
    {
        // Prune expired entries. Only remove the exact key/value pair observed during enumeration, so a
        // concurrently stored fresh entry for the same partition key is never removed by this pass.
        foreach (var kvp in _entries)
        {
            if (kvp.Value.Expires < now)
            {
                RemoveExact(kvp.Key, kvp.Value);
            }
        }

        _entries[partitionKey] = new CacheEntry(keys, expires);
    }

    /// <summary>
    /// Atomically removes the specified key from the dictionary only if it still maps to the exact entry instance
    /// provided, avoiding races with concurrent updates to the same key.
    /// </summary>
    private void RemoveExact(string partitionKey, CacheEntry entry) =>
        ((ICollection<KeyValuePair<string, CacheEntry>>)_entries).Remove(new KeyValuePair<string, CacheEntry>(partitionKey, entry));
}
