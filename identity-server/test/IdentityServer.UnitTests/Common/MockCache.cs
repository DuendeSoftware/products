// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockCache<T> : ICache<T>
    where T : class
{
    public Dictionary<string, T> Items { get; set; } = new Dictionary<string, T>();


    public Task<T> GetAsync(string key)
    {
        Items.TryGetValue(key, out var item);
        return Task.FromResult(item);
    }

    public async Task<T> GetOrAddAsync(string key, TimeSpan duration, Func<Task<T>> get)
    {
        var item = await GetAsync(key);
        if (item == null)
        {
            item = await get();
            await SetAsync(key, item, duration);
        }
        return item;
    }

    public Task RemoveAsync(string key)
    {
        Items.Remove(key);
        return Task.CompletedTask;
    }

    public Task SetAsync(string key, T item, TimeSpan expiration)
    {
        Items[key] = item;
        return Task.CompletedTask;
    }
}
