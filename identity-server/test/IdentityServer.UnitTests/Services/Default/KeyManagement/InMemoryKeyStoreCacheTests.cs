// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services.KeyManagement;
using Duende.Spaces;

namespace UnitTests.Services.Default.KeyManagement;

public class InMemoryKeyStoreCacheTests
{
    private InMemoryKeyStoreCache _subject;
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private FakeTimeProvider _mockTimeProvider = new FakeTimeProvider(new DateTimeOffset(new DateTime(2018, 3, 1, 9, 0, 0)));
    private InMemoryKeyStoreCacheState _state = new InMemoryKeyStoreCacheState();

    public InMemoryKeyStoreCacheTests() => _subject = new InMemoryKeyStoreCache(_mockTimeProvider, _state, null);

    [Fact]
    public async Task GetKeysAsync_within_expiration_should_return_keys()
    {
        var now = _mockTimeProvider.GetUtcNow();

        var keys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(2)) },
        };
        await _subject.StoreKeysAsync(keys, TimeSpan.FromMinutes(1), _ct);

        var result = await _subject.GetKeysAsync(_ct);
        result.ShouldBeSameAs(keys);

        // Verify keys remain cached as time advances within expiration window
        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromSeconds(59)));
        result = await _subject.GetKeysAsync(_ct);
        result.ShouldBeSameAs(keys);

        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromMinutes(1)));
        result = await _subject.GetKeysAsync(_ct);
        result.ShouldBeSameAs(keys);
    }

    [Fact]
    public async Task GetKeysAsync_past_expiration_should_return_no_keys()
    {
        var now = _mockTimeProvider.GetUtcNow();

        var keys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(2)) },
        };
        await _subject.StoreKeysAsync(keys, TimeSpan.FromMinutes(1), _ct);

        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromSeconds(61)));
        var result = await _subject.GetKeysAsync(_ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Two_spaces_store_and_retrieve_independently()
    {
        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();

        var accessorA = new TestSpaceContextAccessor(spaceA);
        var accessorB = new TestSpaceContextAccessor(spaceB);

        var cacheA = new InMemoryKeyStoreCache(_mockTimeProvider, _state, accessorA);
        var cacheB = new InMemoryKeyStoreCache(_mockTimeProvider, _state, accessorB);

        var keysA = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
        };
        var keysB = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(2)) },
        };

        await cacheA.StoreKeysAsync(keysA, TimeSpan.FromMinutes(5), _ct);
        await cacheB.StoreKeysAsync(keysB, TimeSpan.FromMinutes(5), _ct);

        var resultA = await cacheA.GetKeysAsync(_ct);
        var resultB = await cacheB.GetKeysAsync(_ct);

        resultA.ShouldBeSameAs(keysA);
        resultB.ShouldBeSameAs(keysB);
    }

    [Fact]
    public async Task Expired_entry_returns_null_and_is_removed()
    {
        var now = _mockTimeProvider.GetUtcNow();

        var keys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
        };
        await _subject.StoreKeysAsync(keys, TimeSpan.FromMinutes(1), _ct);

        // Advance time past expiration
        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromMinutes(2)));

        // First call should return null and remove the entry
        var result = await _subject.GetKeysAsync(_ct);
        result.ShouldBeNull();

        // Second call should also return null (entry was removed)
        result = await _subject.GetKeysAsync(_ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Write_prunes_stale_entries()
    {
        var now = _mockTimeProvider.GetUtcNow();

        // Create multiple partitions with different accessors
        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();
        var accessorA = new TestSpaceContextAccessor(spaceA);
        var accessorB = new TestSpaceContextAccessor(spaceB);
        var cacheA = new InMemoryKeyStoreCache(_mockTimeProvider, _state, accessorA);
        var cacheB = new InMemoryKeyStoreCache(_mockTimeProvider, _state, accessorB);

        var keysA = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
        };
        var keysB = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(2)) },
        };

        // Store keys in both partitions with short expiration
        await cacheA.StoreKeysAsync(keysA, TimeSpan.FromMinutes(1), _ct);
        await cacheB.StoreKeysAsync(keysB, TimeSpan.FromMinutes(1), _ct);

        // Verify both are cached
        var resultA = await cacheA.GetKeysAsync(_ct);
        var resultB = await cacheB.GetKeysAsync(_ct);
        resultA.ShouldBeSameAs(keysA);
        resultB.ShouldBeSameAs(keysB);

        // Advance time past expiration
        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromMinutes(2)));

        // Store new keys in partition A - this should prune stale entries including B
        var newKeysA = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime },
        };
        await cacheA.StoreKeysAsync(newKeysA, TimeSpan.FromMinutes(5), _ct);

        // Partition A should have new keys
        resultA = await cacheA.GetKeysAsync(_ct);
        resultA.ShouldBeSameAs(newKeysA);

        // Partition B should return null (was pruned during A's write)
        resultB = await cacheB.GetKeysAsync(_ct);
        resultB.ShouldBeNull();
    }

    [Fact]
    public async Task Null_accessor_uses_default_partition()
    {
        var cache1 = new InMemoryKeyStoreCache(_mockTimeProvider, _state, null);
        var cache2 = new InMemoryKeyStoreCache(_mockTimeProvider, _state, null);

        var keys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
        };

        await cache1.StoreKeysAsync(keys, TimeSpan.FromMinutes(5), _ct);

        // cache2 should see the same keys since both use the default partition
        var result = await cache2.GetKeysAsync(_ct);
        result.ShouldBeSameAs(keys);
    }

    [Fact]
    public async Task Write_prune_does_not_remove_entry_refreshed_after_enumeration_snapshot()
    {
        var now = _mockTimeProvider.GetUtcNow();

        var spaceA = SpaceId.New();
        var accessorA = new TestSpaceContextAccessor(spaceA);
        var cacheA = new InMemoryKeyStoreCache(_mockTimeProvider, _state, accessorA);

        var staleKeys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = now.UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
        };
        await cacheA.StoreKeysAsync(staleKeys, TimeSpan.FromMinutes(1), _ct);

        // Advance time so the stored entry for partition A is now stale.
        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromMinutes(2)));

        // Simulate another writer refreshing partition A's entry concurrently with a pruning pass elsewhere:
        // storing keys for partition A directly on the shared state replaces the stale entry instance.
        var freshKeys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime },
        };
        _state.StoreKeys(spaceA.Value.ToString(), freshKeys, _mockTimeProvider.GetUtcNow().UtcDateTime, _mockTimeProvider.GetUtcNow().UtcDateTime.Add(TimeSpan.FromMinutes(5)));

        // Now trigger a prune pass (via a StoreKeys call for a different partition) that, if buggy, would
        // unconditionally remove partition A's entry because it was observed as stale before the refresh above.
        // Since our helper only removes the exact entry instance, the freshly stored entry for A must survive.
        var spaceB = SpaceId.New();
        var accessorB = new TestSpaceContextAccessor(spaceB);
        var cacheB = new InMemoryKeyStoreCache(_mockTimeProvider, _state, accessorB);
        var keysB = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime },
        };
        await cacheB.StoreKeysAsync(keysB, TimeSpan.FromMinutes(5), _ct);

        var resultA = await cacheA.GetKeysAsync(_ct);
        resultA.ShouldBeSameAs(freshKeys);
    }

    private class TestSpaceContextAccessor(SpaceId spaceId) : ISpaceContextAccessor
    {
        public SpaceId GetSpaceId() => spaceId;
        public bool IsSpaceIdConfigured() => true;
        public IDisposable SetSpace(SpaceId spaceId) => NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose() { }
    }
}
