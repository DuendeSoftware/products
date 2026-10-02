// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.Spaces;

/// <summary>
/// A <see cref="HybridCache"/> decorator that re-establishes the ambient space inside the cache factory delegate.
/// </summary>
/// <remarks>
/// <c>DefaultHybridCache</c> can run the factory via <c>ThreadPool.UnsafeQueueUserWorkItem</c> when the token is
/// cancellable, which does not flow <see cref="ExecutionContext"/>, so the <see cref="AsyncLocal{T}"/>-backed space
/// is lost; this decorator re-captures and re-establishes it. Because stampede protection shares one factory run
/// between callers, the cache key must also be space-partitioned by the caller.
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
    /// even if it's queued to the thread pool.
    /// </summary>
    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var expectedSpace = _spaceContextAccessor.IsSpaceIdConfigured() ? _spaceContextAccessor.GetSpaceId() : (SpaceId?)null;

        var wrappedState = (spaceContextAccessor: _spaceContextAccessor, expectedSpace, innerState: state, factory);

        return _inner.GetOrCreateAsync(
            key,
            wrappedState,
            static (wrapped, innerToken) => RunFactoryInExpectedSpaceAsync(
                wrapped.spaceContextAccessor,
                wrapped.expectedSpace,
                wrapped.innerState,
                wrapped.factory,
                innerToken),
            options,
            tags,
            cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        _inner.SetAsync(key, value, options, tags, cancellationToken);

#pragma warning disable RS0026 // preserves HybridCache's optional-parameter signatures
    /// <inheritdoc/>
    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.RemoveAsync(key, cancellationToken);

    /// <inheritdoc/>
    public override ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) =>
        _inner.RemoveAsync(keys, cancellationToken);

    /// <inheritdoc/>
    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        _inner.RemoveByTagAsync(tag, cancellationToken);

    /// <inheritdoc/>
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
}
