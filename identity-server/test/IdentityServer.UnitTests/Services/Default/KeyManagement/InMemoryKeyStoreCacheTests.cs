// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services.KeyManagement;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Services.Default.KeyManagement;

public class InMemoryKeyStoreCacheTests
{
    private InMemoryKeyStoreCache _subject;
    private FakeTimeProvider _mockTimeProvider = new FakeTimeProvider(new DateTimeOffset(new DateTime(2018, 3, 1, 9, 0, 0)));

    public InMemoryKeyStoreCacheTests() => _subject = new InMemoryKeyStoreCache(_mockTimeProvider);

    [Fact]
    public async Task GetKeysAsync_within_expiration_should_return_keys()
    {
        var now = _mockTimeProvider.GetUtcNow();

        var keys = new RsaKeyContainer[] {
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(1)) },
            new RsaKeyContainer() { Created = _mockTimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromMinutes(2)) },
        };
        await _subject.StoreKeysAsync(keys, TimeSpan.FromMinutes(1));

        var result = await _subject.GetKeysAsync();
        result.ShouldBeSameAs(keys);

        // Verify keys remain cached as time advances within expiration window
        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromSeconds(59)));
        result = await _subject.GetKeysAsync();
        result.ShouldBeSameAs(keys);

        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromMinutes(1)));
        result = await _subject.GetKeysAsync();
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
        await _subject.StoreKeysAsync(keys, TimeSpan.FromMinutes(1));

        _mockTimeProvider.SetUtcNow(now.Add(TimeSpan.FromSeconds(61)));
        var result = await _subject.GetKeysAsync();
        result.ShouldBeNull();
    }
}
