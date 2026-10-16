// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageServerSideSessionStoreContractTests : ServerSideSessionStoreContractTests
{
    private readonly string _dbName = $"StorageServerSideSessionContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync() => _provider = await StorageContractTestServices.CreateAsync(_dbName, b => b.AddOperationalStorage().AddServerSideSessions());

    public override async ValueTask DisposeAsync()
    {
        try
        {
            DataCategoryNameRecorder.CurrentTestShouldHaveUsed([_provider], DataCategoryName.Operational);
        }
        finally
        {
            await _provider.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    protected override StoreHandle<IServerSideSessionStore> CreateStore() => new StoreHandle<IServerSideSessionStore>(_provider.GetRequiredService<IServerSideSessionStore>());
}
