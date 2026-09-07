// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for ISamlServiceProviderStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing store
/// starts empty for each test — providing natural test isolation.
/// </summary>
public class InMemorySamlServiceProviderStoreContractTests : SamlServiceProviderStoreContractTests
{
    private readonly List<SamlServiceProvider> _serviceProviders = [];

    protected override StoreHandle<ISamlServiceProviderStore> CreateStore() =>
        new(new InMemorySamlServiceProviderStore(_serviceProviders));

    protected override Task<StoreHandle<ISamlServiceProviderStore>> CreateIsolatedStoreAsync() =>
        Task.FromResult(new StoreHandle<ISamlServiceProviderStore>(new InMemorySamlServiceProviderStore([])));

    protected override Task SeedSamlServiceProviderAsync(SamlServiceProvider sp)
    {
        _serviceProviders.Add(sp);
        return Task.CompletedTask;
    }
}
