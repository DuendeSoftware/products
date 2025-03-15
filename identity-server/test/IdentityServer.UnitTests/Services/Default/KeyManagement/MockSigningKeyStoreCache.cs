// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services.KeyManagement;

namespace UnitTests.Services.Default.KeyManagement;

internal class MockSigningKeyStoreCache : ISigningKeyStoreCache
{
    public List<KeyContainer> Cache { get; set; } = new List<KeyContainer>();

    public bool GetKeysAsyncWasCalled { get; set; }
    public bool StoreKeysAsyncWasCalled { get; set; }
    public TimeSpan StoreKeysAsyncDuration { get; set; }

    public Task<IEnumerable<KeyContainer>> GetKeysAsync()
    {
        GetKeysAsyncWasCalled = true;
        return Task.FromResult(Cache.AsEnumerable());
    }

    public Task StoreKeysAsync(IEnumerable<KeyContainer> keys, TimeSpan duration)
    {
        StoreKeysAsyncWasCalled = true;
        StoreKeysAsyncDuration = duration;

        Cache = keys.ToList();
        return Task.CompletedTask;
    }
}
