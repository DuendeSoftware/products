// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageSamlSigninStateStoreContractTests : SamlSigninStateStoreContractTests
{
    private readonly string _dbName = $"StorageSamlSigninStateContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync() => _provider = await StorageContractTestServices.CreateAsync(_dbName, b => b.AddOperationalStorage());

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

    protected override StoreHandle<ISamlSigninStateStore> CreateStore() => new StoreHandle<ISamlSigninStateStore>(_provider.GetRequiredService<ISamlSigninStateStore>());
}
