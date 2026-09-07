// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for IPersistedGrantStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing store
/// starts empty for each test — providing natural test isolation.
/// </summary>
public class InMemoryPersistedGrantStoreContractTests : PersistedGrantStoreContractTests
{
    protected override StoreHandle<IPersistedGrantStore> CreateStore() =>
        new(new InMemoryPersistedGrantStore());

    protected override Task<StoreHandle<IPersistedGrantStore>> CreateIsolatedStoreAsync() =>
        Task.FromResult(new StoreHandle<IPersistedGrantStore>(new InMemoryPersistedGrantStore()));
}
