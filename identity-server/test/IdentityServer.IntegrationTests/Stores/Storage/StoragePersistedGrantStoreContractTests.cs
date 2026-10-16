// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StoragePersistedGrantStoreContractTests : PersistedGrantStoreContractTests
{
    private readonly string _dbName = $"StoragePersistedGrantContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;
    private readonly List<ServiceProvider> _isolatedProviders = [];

    public override async ValueTask InitializeAsync() => _provider = await StorageContractTestServices.CreateAsync(_dbName, b => b.AddOperationalStorage());

    public override async ValueTask DisposeAsync()
    {
        try
        {
            DataCategoryNameRecorder.CurrentTestShouldHaveUsed([.. _isolatedProviders, _provider], DataCategoryName.Operational);
        }
        finally
        {
            foreach (var provider in _isolatedProviders)
            {
                await provider.DisposeAsync();
            }
            await _provider.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    protected override StoreHandle<IPersistedGrantStore> CreateStore() => new StoreHandle<IPersistedGrantStore>(_provider.GetRequiredService<IPersistedGrantStore>());

    protected override async Task<StoreHandle<IPersistedGrantStore>> CreateIsolatedStoreAsync()
    {
        var isolatedDbName = $"StoragePersistedGrantContractIsolated_{Guid.NewGuid():N}";
        var provider = await StorageContractTestServices.CreateAsync(isolatedDbName, b => b.AddOperationalStorage());
        _isolatedProviders.Add(provider);

        return new StoreHandle<IPersistedGrantStore>(provider.GetRequiredService<IPersistedGrantStore>());
    }
}
