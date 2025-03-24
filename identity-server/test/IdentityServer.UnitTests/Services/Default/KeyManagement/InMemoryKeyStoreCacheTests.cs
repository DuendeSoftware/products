// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services.KeyManagement;

namespace UnitTests.Services.Default.KeyManagement;

public class InMemoryKeyStoreCacheTests
{
    private InMemoryKeyStoreCache _subject;
    private MockClock _mockClock = new MockClock(new DateTime(2018, 3, 1, 9, 0, 0));

    public InMemoryKeyStoreCacheTests()
    {
        _subject = new InMemoryKeyStoreCache(_mockClock);
    }

    [Fact]
    public async Task GetKeysAsync_within_expiration_should_return_keys()
    {
        var now = _mockClock.UtcNow;

        var keys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockClock.UtcNow.UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
            new RsaKeyContainer() { Created = _mockClock.UtcNow.UtcDateTime.Subtract(TimeSpan.FromMinutes(2)) },
        };
        await _subject.StoreKeysAsync(keys, TimeSpan.FromMinutes(1));

        var result = await _subject.GetKeysAsync();
        result.ShouldBeSameAs(keys);

        _mockClock.UtcNow = now.Subtract(TimeSpan.FromDays(1));
        result = await _subject.GetKeysAsync();
        result.ShouldBeSameAs(keys);

        _mockClock.UtcNow = now.Add(TimeSpan.FromSeconds(59));
        result = await _subject.GetKeysAsync();
        result.ShouldBeSameAs(keys);

        _mockClock.UtcNow = now.Add(TimeSpan.FromMinutes(1));
        result = await _subject.GetKeysAsync();
        result.ShouldBeSameAs(keys);
    }

    [Fact]
    public async Task GetKeysAsync_past_expiration_should_return_no_keys()
    {
        var now = _mockClock.UtcNow;

        var keys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockClock.UtcNow.UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
            new RsaKeyContainer() { Created = _mockClock.UtcNow.UtcDateTime.Subtract(TimeSpan.FromMinutes(2)) },
        };
        await _subject.StoreKeysAsync(keys, TimeSpan.FromMinutes(1));

        _mockClock.UtcNow = now.Add(TimeSpan.FromSeconds(61));
        var result = await _subject.GetKeysAsync();
        result.ShouldBeNull();
    }
}
