// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageIdentityProviderStoreContractTests : IdentityProviderStoreContractTests
{
    private readonly string _dbName = $"StorageIdentityProviderContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<IdentityProviderDso.V1>();
        services.AddSingleton<IPoolContextAccessor, PoolContextAccessor>();
        services.AddSingleton<IStorageFactory, DefaultStorageFactory>();
        services.AddScoped<IdentityProviderRepository>();
        services.AddSingleton<IIdentityProviderFactory, TestIdentityProviderFactory>();
        services.AddScoped<IIdentityProviderStore, IdentityProviderStore>();

        _provider = services.BuildServiceProvider();

        var schema = _provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override StoreHandle<IIdentityProviderStore> CreateStore()
    {
        var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdentityProviderStore>();
        return StoreHandle.WithDisposable(store, scope);
    }

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

    protected override async Task SeedIdentityProviderAsync(IdentityProvider idp)
    {
        await using var scope = _provider.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IdentityProviderRepository>();
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
