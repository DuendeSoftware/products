// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

/// <summary>
/// Tests for <see cref="SpaceAwareHybridCache"/>: it exists because <c>DefaultHybridCache</c> can
/// queue its factory to the thread pool (dropping the ambient space) when the token is cancellable.
/// </summary>
public sealed class SpaceAwareHybridCacheTests : IDisposable
{
    private static readonly AsyncLocal<string?> UnrelatedAmbientValue = new();

    private readonly ServiceProvider _services;
    private readonly ISpaceContextAccessor _accessor;
    private readonly HybridCache _innerCache;
    private readonly SpaceAwareHybridCache _cache;

    public SpaceAwareHybridCacheTests()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        TestSpacesLicense.RegisterEntitled(sc);
        _services = sc.BuildServiceProvider();
        _accessor = _services.GetRequiredService<ISpaceContextAccessor>();
        _innerCache = _services.GetRequiredService<HybridCache>();
        _cache = new SpaceAwareHybridCache(_innerCache, _accessor);
    }

    public void Dispose() => _services.Dispose();

    private static string NewKey([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid()}";

    [Fact]
    public void ctor_throws_when_inner_is_null() =>
        Should.Throw<ArgumentNullException>(() => new SpaceAwareHybridCache(null!, _accessor));

    [Fact]
    public void ctor_throws_when_space_context_accessor_is_null() =>
        Should.Throw<ArgumentNullException>(() => new SpaceAwareHybridCache(_innerCache, null!));

    [Fact]
    public async Task factory_observes_callers_space_with_cancellable_token()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);
        using var cts = new CancellationTokenSource();

        var observed = await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: async (_, _) =>
            {
                await Task.Yield();
                return _accessor.IsSpaceIdConfigured() ? _accessor.GetSpaceId().Value.ToString() : null;
            },
            cancellationToken: cts.Token);

        observed.ShouldBe(spaceId.Value.ToString());
    }

    [Fact]
    public async Task factory_observes_callers_space_with_non_cancellable_token()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);

        var observed = await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: async (_, _) =>
            {
                await Task.Yield();
                return _accessor.IsSpaceIdConfigured() ? _accessor.GetSpaceId().Value.ToString() : null;
            },
            cancellationToken: CancellationToken.None);

        observed.ShouldBe(spaceId.Value.ToString());
    }

    [Fact]
    public async Task stateless_overload_also_observes_callers_space()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);
        using var cts = new CancellationTokenSource();

        var observed = await _cache.GetOrCreateAsync(
            NewKey(),
            factory: async _ =>
            {
                await Task.Yield();
                return _accessor.IsSpaceIdConfigured() ? _accessor.GetSpaceId().Value.ToString() : null;
            },
            cancellationToken: cts.Token);

        observed.ShouldBe(spaceId.Value.ToString());
    }

    /// <summary>
    /// Guards that plain <c>HybridCache.GetOrCreateAsync</c> on the undecorated cache does not
    /// expose the space when the token is cancellable; if this starts passing, the workaround
    /// may no longer be needed.
    /// </summary>
    [Fact]
    public async Task plain_inner_GetOrCreateAsync_does_not_expose_callers_space_when_token_is_cancellable()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);
        using var cts = new CancellationTokenSource();

        var observed = await _innerCache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: async (_, _) =>
            {
                await Task.Yield();
                return _accessor.IsSpaceIdConfigured() ? _accessor.GetSpaceId().Value.ToString() : null;
            },
            cancellationToken: cts.Token);

        observed.ShouldBeNull();
    }

    [Fact]
    public async Task only_the_space_flows_not_unrelated_ambient_state()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);
        UnrelatedAmbientValue.Value = "caller-value";
        using var cts = new CancellationTokenSource();

        var observed = await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: async (_, _) =>
            {
                await Task.Yield();
                return UnrelatedAmbientValue.Value;
            },
            cancellationToken: cts.Token);

        observed.ShouldBeNull();
    }

    [Fact]
    public async Task callers_space_context_is_unchanged_after_the_call()
    {
        var callerSpace = SpaceId.New();
        using var scope = _accessor.SetSpace(callerSpace);
        using var cts = new CancellationTokenSource();

        _ = await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: static async (_, _) =>
            {
                await Task.Yield();
                return 42;
            },
            cancellationToken: cts.Token);

        _accessor.IsSpaceIdConfigured().ShouldBeTrue();
        _accessor.GetSpaceId().ShouldBe(callerSpace);
    }

    [Fact]
    public async Task space_set_inside_factory_does_not_leak_to_a_caller_with_no_space()
    {
        var spaceId = SpaceId.New();
        using (_accessor.SetSpace(spaceId))
        {
            using var cts = new CancellationTokenSource();
            _ = await _cache.GetOrCreateAsync(
                NewKey(),
                state: 0,
                factory: static async (_, _) =>
                {
                    await Task.Yield();
                    return 1;
                },
                cancellationToken: cts.Token);
        }

        // Back on the caller's flow with no space configured; must not observe the space set inside the previous factory.
        using var secondCts = new CancellationTokenSource();
        var observedInSecondRun = await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: async (_, _) =>
            {
                await Task.Yield();
                return _accessor.IsSpaceIdConfigured() ? _accessor.GetSpaceId().Value.ToString() : null;
            },
            cancellationToken: secondCts.Token);

        observedInSecondRun.ShouldBeNull();
    }

    [Fact]
    public async Task no_space_configured_on_caller_runs_factory_with_no_space_and_no_exception()
    {
        _accessor.IsSpaceIdConfigured().ShouldBeFalse();
        using var cts = new CancellationTokenSource();

        var observed = await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: async (_, _) =>
            {
                await Task.Yield();
                return _accessor.IsSpaceIdConfigured() ? _accessor.GetSpaceId().Value.ToString() : null;
            },
            cancellationToken: cts.Token);

        observed.ShouldBeNull();
    }

    /// <summary>
    /// Fake <see cref="HybridCache"/> simulating a factory run that ends up with a different
    /// space than the caller's; there's no deterministic way to trigger this with a real
    /// <see cref="HybridCache"/>.
    /// </summary>
    [Fact]
    public async Task throws_when_factory_observes_a_different_space_than_the_caller_expected()
    {
        var callerSpace = SpaceId.New();
        var corruptingSpace = SpaceId.New();
        using var scope = _accessor.SetSpace(callerSpace);

        var corruptingCache = new SpaceAwareHybridCache(
            new SpaceCorruptingFakeHybridCache(_accessor, corruptingSpace),
            _accessor);

        var act = async () => await corruptingCache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: static (_, _) => ValueTask.FromResult(1),
            cancellationToken: CancellationToken.None);

        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain(callerSpace.Value.ToString());
        exception.Message.ShouldContain(corruptingSpace.Value.ToString());
    }

    /// <summary>Same fake, but caller expected no space.</summary>
    [Fact]
    public async Task throws_when_factory_observes_a_space_but_caller_expected_none()
    {
        var corruptingSpace = SpaceId.New();
        _accessor.IsSpaceIdConfigured().ShouldBeFalse();

        var corruptingCache = new SpaceAwareHybridCache(
            new SpaceCorruptingFakeHybridCache(_accessor, corruptingSpace),
            _accessor);

        var act = async () => await corruptingCache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: static (_, _) => ValueTask.FromResult(1),
            cancellationToken: CancellationToken.None);

        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("none");
        exception.Message.ShouldContain(corruptingSpace.Value.ToString());
    }

    [Fact]
    public async Task synchronous_exception_from_factory_propagates_and_disposes_the_space_scope()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);
        using var cts = new CancellationTokenSource();

        // Cancellable token forces the thread-pool path (no space flows in), so the wrapper must open its own scope.
        var act = async () => await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: static ValueTask<int> (_, _) => throw new InvalidOperationException("sync boom"),
            cancellationToken: cts.Token);

        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("sync boom");

        _accessor.IsSpaceIdConfigured().ShouldBeTrue();
        _accessor.GetSpaceId().ShouldBe(spaceId);
    }

    [Fact]
    public async Task asynchronous_exception_from_factory_propagates_and_disposes_the_space_scope()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);
        using var cts = new CancellationTokenSource();

        var act = async () => await _cache.GetOrCreateAsync(
            NewKey(),
            state: 0,
            factory: static async (_, _) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async boom");
#pragma warning disable CS0162 // Unreachable code detected
                return 0;
#pragma warning restore CS0162
            },
            cancellationToken: cts.Token);

        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("async boom");

        _accessor.IsSpaceIdConfigured().ShouldBeTrue();
        _accessor.GetSpaceId().ShouldBe(spaceId);
    }

    [Fact]
    public async Task cache_hit_does_not_invoke_the_factory()
    {
        var spaceId = SpaceId.New();
        using var scope = _accessor.SetSpace(spaceId);
        var key = NewKey();
        var invocationCount = 0;

        Func<int, CancellationToken, ValueTask<int>> factory = async (state, _) =>
        {
            Interlocked.Increment(ref invocationCount);
            await Task.Yield();
            return state;
        };

        var first = await _cache.GetOrCreateAsync(key, 1, factory, cancellationToken: CancellationToken.None);
        var second = await _cache.GetOrCreateAsync(key, 2, factory, cancellationToken: CancellationToken.None);

        first.ShouldBe(1);
        second.ShouldBe(1);
        invocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task SetAsync_reaches_the_inner_cache()
    {
        var inner = new RecordingFakeHybridCache();
        var cache = new SpaceAwareHybridCache(inner, _accessor);

        await cache.SetAsync("key", "value");

        inner.SetAsyncCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAsync_single_key_reaches_the_inner_cache()
    {
        var inner = new RecordingFakeHybridCache();
        var cache = new SpaceAwareHybridCache(inner, _accessor);

        await cache.RemoveAsync("key");

        inner.RemoveAsyncSingleCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAsync_multiple_keys_reaches_the_inner_cache()
    {
        var inner = new RecordingFakeHybridCache();
        var cache = new SpaceAwareHybridCache(inner, _accessor);

        await cache.RemoveAsync(["key1", "key2"]);

        inner.RemoveAsyncMultiCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveByTagAsync_single_tag_reaches_the_inner_cache()
    {
        var inner = new RecordingFakeHybridCache();
        var cache = new SpaceAwareHybridCache(inner, _accessor);

        await cache.RemoveByTagAsync("tag");

        inner.RemoveByTagAsyncSingleCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveByTagAsync_multiple_tags_reaches_the_inner_cache()
    {
        var inner = new RecordingFakeHybridCache();
        var cache = new SpaceAwareHybridCache(inner, _accessor);

        await cache.RemoveByTagAsync(["tag1", "tag2"]);

        inner.RemoveByTagAsyncMultiCalls.ShouldBe(1);
    }

    /// <summary>Fake <see cref="HybridCache"/> that forces a different space during the factory run.</summary>
    private sealed class SpaceCorruptingFakeHybridCache(ISpaceContextAccessor accessor, SpaceId corruptingSpace) : HybridCache
    {
        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            using var corrupted = accessor.SetSpace(corruptingSpace);
            return factory(state, cancellationToken);
        }

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    /// <summary>Fake <see cref="HybridCache"/> recording calls to verify forwarding to the inner cache.</summary>
    private sealed class RecordingFakeHybridCache : HybridCache
    {
        public int SetAsyncCalls { get; private set; }

        public int RemoveAsyncSingleCalls { get; private set; }

        public int RemoveAsyncMultiCalls { get; private set; }

        public int RemoveByTagAsyncSingleCalls { get; private set; }

        public int RemoveByTagAsyncMultiCalls { get; private set; }

        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default) => factory(state, cancellationToken);

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            SetAsyncCalls++;
            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemoveAsyncSingleCalls++;
            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            RemoveAsyncMultiCalls++;
            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            RemoveByTagAsyncSingleCalls++;
            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            RemoveByTagAsyncMultiCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
