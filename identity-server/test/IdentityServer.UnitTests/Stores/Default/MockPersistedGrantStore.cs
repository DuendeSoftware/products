// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Stores.Default;

internal class MockPersistedGrantStore : IPersistedGrantStore
{
    public List<PersistedGrantFilter> RemovedFilters { get; } = [];

    public Task StoreAsync(PersistedGrant grant, Ct ct) => Task.CompletedTask;

    public Task<PersistedGrant> GetAsync(string key, Ct ct) => Task.FromResult<PersistedGrant>(null);

    public Task<IReadOnlyCollection<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, Ct ct) =>
        Task.FromResult<IReadOnlyCollection<PersistedGrant>>([]);

    public Task RemoveAsync(string key, Ct ct) => Task.CompletedTask;

    public Task RemoveAllAsync(PersistedGrantFilter filter, Ct ct)
    {
        RemovedFilters.Add(filter);
        return Task.CompletedTask;
    }
}
