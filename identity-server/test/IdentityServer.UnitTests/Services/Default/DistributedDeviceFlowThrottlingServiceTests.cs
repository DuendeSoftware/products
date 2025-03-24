// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Distributed;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DistributedDeviceFlowThrottlingServiceTests
{
    private TestCache cache = new TestCache();
    private InMemoryClientStore _store;

    private readonly IdentityServerOptions options = new IdentityServerOptions { DeviceFlow = new DeviceFlowOptions { Interval = 5 } };
    private readonly DeviceCode deviceCode = new DeviceCode
    {
        Lifetime = 300,
        CreationTime = DateTime.UtcNow
    };

    private const string CacheKey = "devicecode_";
    private readonly DateTime testDate = new DateTime(2018, 09, 01, 8, 0, 0, DateTimeKind.Utc);

    public DistributedDeviceFlowThrottlingServiceTests()
    {
        _store = new InMemoryClientStore(new List<Client>());
    }

    [Fact]
    public async Task First_Poll()
    {
        var handle = Guid.NewGuid().ToString();
        var service = new DistributedDeviceFlowThrottlingService(cache, _store, new StubClock { UtcNowFunc = () => testDate }, options);

        var result = await service.ShouldSlowDown(handle, deviceCode);

        result.ShouldBeFalse();

        CheckCacheEntry(handle);
    }

    [Fact]
    public async Task Second_Poll_Too_Fast()
    {
        var handle = Guid.NewGuid().ToString();
        var service = new DistributedDeviceFlowThrottlingService(cache, _store, new StubClock { UtcNowFunc = () => testDate }, options);

        cache.Set(CacheKey + handle, Encoding.UTF8.GetBytes(testDate.AddSeconds(-1).ToString("O")));

        var result = await service.ShouldSlowDown(handle, deviceCode);

        result.ShouldBeTrue();

        CheckCacheEntry(handle);
    }

    [Fact]
    public async Task Second_Poll_After_Interval()
    {
        var handle = Guid.NewGuid().ToString();

        var service = new DistributedDeviceFlowThrottlingService(cache, _store, new StubClock { UtcNowFunc = () => testDate }, options);

        cache.Set($"devicecode_{handle}", Encoding.UTF8.GetBytes(testDate.AddSeconds(-deviceCode.Lifetime - 1).ToString("O")));

        var result = await service.ShouldSlowDown(handle, deviceCode);

        result.ShouldBeFalse();

        CheckCacheEntry(handle);
    }

    /// <summary>
    /// Addresses race condition from #3860
    /// </summary>
    [Fact]
    public async Task Expired_Device_Code_Should_Not_Have_Expiry_in_Past()
    {
        var handle = Guid.NewGuid().ToString();
        deviceCode.CreationTime = testDate.AddSeconds(-deviceCode.Lifetime * 2);

        var service = new DistributedDeviceFlowThrottlingService(cache, _store, new StubClock { UtcNowFunc = () => testDate }, options);

        var result = await service.ShouldSlowDown(handle, deviceCode);

        result.ShouldBeFalse();

        cache.Items.TryGetValue(CacheKey + handle, out var values).ShouldBeTrue();
        values?.Item2.AbsoluteExpiration.Value.ShouldBeGreaterThanOrEqualTo(testDate);
    }

    private void CheckCacheEntry(string handle)
    {
        cache.Items.TryGetValue(CacheKey + handle, out var values).ShouldBeTrue();

        var dateTimeAsString = Encoding.UTF8.GetString(values?.Item1);
        var dateTime = DateTime.Parse(dateTimeAsString).ToUniversalTime();
        dateTime.ShouldBe(testDate);

        values?.Item2.AbsoluteExpiration.Value.ShouldBeCloseTo(testDate.AddSeconds(deviceCode.Lifetime), TimeSpan.FromMicroseconds(1));
    }
}

internal class TestCache : IDistributedCache
{
    public readonly Dictionary<string, Tuple<byte[], DistributedCacheEntryOptions>> Items =
        new Dictionary<string, Tuple<byte[], DistributedCacheEntryOptions>>();

    public byte[] Get(string key)
    {
        if (Items.TryGetValue(key, out var value)) return value.Item1;
        return null;
    }

    public Task<byte[]> GetAsync(string key, CancellationToken token = new CancellationToken())
    {
        return Task.FromResult(Get(key));
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        Items.Remove(key);

        Items.Add(key, new Tuple<byte[], DistributedCacheEntryOptions>(value, options));
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = new CancellationToken())
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        throw new NotImplementedException();
    }

    public Task RefreshAsync(string key, CancellationToken token = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public void Remove(string key)
    {
        throw new NotImplementedException();
    }

    public Task RemoveAsync(string key, CancellationToken token = new CancellationToken())
    {
        throw new NotImplementedException();
    }
}
