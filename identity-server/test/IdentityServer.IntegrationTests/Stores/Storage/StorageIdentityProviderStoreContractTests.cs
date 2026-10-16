// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageIdentityProviderStoreContractTests : IdentityProviderStoreContractTests
{
    private readonly string _dbName = $"StorageIdentityProviderContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync() => _provider = await StorageContractTestServices.CreateAsync(_dbName, b => b.AddConfigurationStorage(), services => services.Replace(ServiceDescriptor.Singleton<IIdentityProviderFactory, TestIdentityProviderFactory>()));

    public override async ValueTask DisposeAsync()
    {
        try
        {
            DataCategoryNameRecorder.CurrentTestShouldHaveUsed([_provider], DataCategoryName.Configuration);
        }
        finally
        {
            await _provider.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    protected override StoreHandle<IIdentityProviderStore> CreateStore() => new StoreHandle<IIdentityProviderStore>(_provider.GetRequiredService<IIdentityProviderStore>());

    /// <summary>
    /// Storage returns the base IdentityProvider when the factory cannot create a typed instance,
    /// rather than returning null like the EF store does.
    /// </summary>
    [Fact]
    public override async Task GetBySchemeAsync_should_filter_by_type()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme2",
            Type = "unknown"
        };
        await SeedIdentityProviderAsync(idp);

        await using var handle = CreateStore();
        var item = await handle.Store.GetBySchemeAsync("scheme2", _ct);

        // Storage does not filter by type — it returns the base provider.
        item.ShouldNotBeNull();
    }

    // NOTE: Seeds through IdentityProviderRepository rather than IIdentityProviderAdmin on purpose.
    // The admin rejects provider types not registered with AddProviderType, while these contract tests
    // deliberately store unregistered types ("test-custom", "unknown") to check how the store handles them.
    protected override async Task SeedIdentityProviderAsync(IdentityProvider idp)
    {
        var repo = _provider.GetRequiredService<IdentityProviderRepository>();
        var dso = new IdentityProviderDso.V1
        {
            Id = Guid.NewGuid(),
            Scheme = idp.Scheme,
            DisplayName = idp.DisplayName,
            Enabled = idp.Enabled,
            Type = idp.Type,
            ExtendedAttributeValues = idp.Properties
                .Select(kvp => new AttributeValueDso.V1(kvp.Key, kvp.Value))
                .ToList()
        };
        await repo.CreateAsync(UuidV7.New(), dso, _ct);
    }
}
