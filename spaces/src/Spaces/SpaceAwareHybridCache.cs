// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.Spaces;

/// <summary>
/// A <see cref="HybridCache"/> decorator that re-establishes the ambient space inside the cache factory delegate,
/// and partitions cache entries by space.
/// </summary>
/// <remarks>
/// <c>DefaultHybridCache</c> can run the factory via <c>ThreadPool.UnsafeQueueUserWorkItem</c> when the token is
/// cancellable, which does not flow <see cref="ExecutionContext"/>, so the <see cref="AsyncLocal{T}"/>-backed space
/// is lost; this decorator re-captures and re-establishes it. It also prefixes every key with the caller's current
/// space (or <see cref="SpaceId.Default"/> when none is set) and tags every write with that space, so callers never
/// need to build space-aware keys themselves, and entries for a space can be evicted together. Tags are not
/// partitioned, so <see cref="RemoveByTagAsync(string, CancellationToken)"/> reaches entries in every space.
/// </remarks>
public sealed class SpaceAwareHybridCache : HybridCache
{
    private const string NoSpaceLabel = "none";

    private readonly HybridCache _inner;
    private readonly ISpaceContextAccessor _spaceContextAccessor;

    /// <summary>
    /// Creates a new <see cref="SpaceAwareHybridCache"/> that wraps <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The cache to delegate to.</param>
    /// <param name="spaceContextAccessor">Used to capture and re-establish the caller's space.</param>
    public SpaceAwareHybridCache(HybridCache inner, ISpaceContextAccessor spaceContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(spaceContextAccessor);

        _inner = inner;
        _spaceContextAccessor = spaceContextAccessor;
    }

    /// <summary>
    /// Calls <c>HybridCache.GetOrCreateAsync</c>, ensuring the factory observes the caller's space
    /// even if it's queued to the thread pool. The key is partitioned by space and the entry is tagged with its
    /// space; caller-supplied tags are not partitioned.
    /// </summary>
    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var spaceId = _spaceContextAccessor.GetSpaceIdOrDefault();
        var expectedSpace = _spaceContextAccessor.IsSpaceIdConfigured() ? _spaceContextAccessor.GetSpaceId() : (SpaceId?)null;

        var wrappedState = (spaceContextAccessor: _spaceContextAccessor, expectedSpace, innerState: state, factory);

        return _inner.GetOrCreateAsync(
            SpaceCacheKeys.ForCacheEntry(spaceId, key),
            wrappedState,
            static (wrapped, innerToken) => RunFactoryInExpectedSpaceAsync(
                wrapped.spaceContextAccessor,
                wrapped.expectedSpace,
                wrapped.innerState,
                wrapped.factory,
                innerToken),
            options,
            WithSpaceTag(tags, spaceId),
            cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var spaceId = _spaceContextAccessor.GetSpaceIdOrDefault();
        return _inner.SetAsync(SpaceCacheKeys.ForCacheEntry(spaceId, key), value, options, WithSpaceTag(tags, spaceId), cancellationToken);
    }

#pragma warning disable RS0026 // preserves HybridCache's optional-parameter signatures
    /// <inheritdoc/>
    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _inner.RemoveAsync(SpaceCacheKeys.ForCacheEntry(_spaceContextAccessor.GetSpaceIdOrDefault(), key), cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        // HybridCache's contract says implementors treat a null sequence as empty.
        if (keys is null)
        {
            return ValueTask.CompletedTask;
        }

        var spaceId = _spaceContextAccessor.GetSpaceIdOrDefault();

        // Materialized eagerly so a null element throws here, at the call site, rather than lazily inside the inner cache.
        var prefixedKeys = new List<string>(keys is ICollection<string> collection ? collection.Count : 4);
        foreach (var key in keys)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(keys));
            prefixedKeys.Add(SpaceCacheKeys.ForCacheEntry(spaceId, key));
        }

        return _inner.RemoveAsync(prefixedKeys, cancellationToken);
    }

    /// <summary>
    /// Removes all entries with <paramref name="tag"/>, in every space.
    /// </summary>
    /// <remarks>
    /// Tags are not partitioned by space, so this is intended for trusted host code, for example to evict a whole
    /// space with its <c>space:{id}</c> tag. Don't pass tags that come from untrusted input.
    /// </remarks>
    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        _inner.RemoveByTagAsync(tag, cancellationToken);

    /// <summary>
    /// Removes all entries with any of <paramref name="tags"/>, in every space.
    /// </summary>
    /// <remarks>
    /// Tags are not partitioned by space, so this is intended for trusted host code, for example to evict a whole
    /// space with its <c>space:{id}</c> tag. Don't pass tags that come from untrusted input.
    /// </remarks>
    public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default) =>
        _inner.RemoveByTagAsync(tags, cancellationToken);
#pragma warning restore RS0026

    private static ValueTask<T> RunFactoryInExpectedSpaceAsync<TState, T>(
        ISpaceContextAccessor spaceContextAccessor,
        SpaceId? expectedSpace,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken)
    {
        // Only meaningful when the space flowed naturally; the thread-pool case is handled below by re-establishing it.
        if (spaceContextAccessor.IsSpaceIdConfigured())
        {
            var actualSpace = spaceContextAccessor.GetSpaceId();

            if (actualSpace != expectedSpace)
            {
                throw new InvalidOperationException(
                    $"HybridCache factory observed space '{actualSpace.Value}' but the caller expected space " +
                    $"'{(expectedSpace is { } id ? id.Value.ToString() : NoSpaceLabel)}'. This indicates the " +
                    "factory ran in an unexpected ambient context.");
            }

            return factory(state, cancellationToken);
        }

        if (expectedSpace is null)
        {
            return factory(state, cancellationToken);
        }

        return RunFactoryWithSpaceSetAsync(spaceContextAccessor, expectedSpace, state, factory, cancellationToken);
    }

    private static async ValueTask<T> RunFactoryWithSpaceSetAsync<TState, T>(
        ISpaceContextAccessor spaceContextAccessor,
        SpaceId spaceToSet,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken)
    {
        using (spaceContextAccessor.SetSpace(spaceToSet))
        {
            return await factory(state, cancellationToken);
        }
    }

    private static IReadOnlyCollection<string> WithSpaceTag(IEnumerable<string>? tags, SpaceId spaceId)
    {
        var spaceTag = SpaceCacheKeys.ForSpaceTag(spaceId);

        if (tags is null)
        {
            return [spaceTag];
        }

        var materialized = tags as IReadOnlyCollection<string> ?? tags.ToArray();
        // HybridCache compares tags ordinally, so don't let the caller's collection comparer decide whether the tag is present.
        return materialized.Contains(spaceTag, StringComparer.Ordinal) ? materialized : [.. materialized, spaceTag];
    }
}
