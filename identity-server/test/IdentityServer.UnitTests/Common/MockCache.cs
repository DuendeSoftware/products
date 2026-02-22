// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockCache<T> : ICache<T>
    where T : class
{
    public Dictionary<string, T> Items { get; set; } = new Dictionary<string, T>();


    public Task<T> GetAsync(string key, Ct ct)
    {
        Items.TryGetValue(key, out var item);
        return Task.FromResult(item);
    }

    public async Task<T> GetOrAddAsync(string key, TimeSpan duration, Func<Task<T>> get, Ct ct)
    {
        var item = await GetAsync(key, ct);
        if (item == null)
        {
            item = await get();
            await SetAsync(key, item, duration, ct);
        }
        return item;
    }

    public Task RemoveAsync(string key, Ct ct)
    {
        Items.Remove(key);
        return Task.CompletedTask;
    }

    public Task SetAsync(string key, T item, TimeSpan expiration, Ct ct)
    {
        Items[key] = item;
        return Task.CompletedTask;
    }
}
