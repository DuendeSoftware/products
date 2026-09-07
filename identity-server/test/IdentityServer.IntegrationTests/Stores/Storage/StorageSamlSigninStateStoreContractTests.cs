// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.SamlSigninState;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageSamlSigninStateStoreContractTests : SamlSigninStateStoreContractTests
{
    private readonly string _dbName = $"StorageSamlSigninStateContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<SamlSigninStateDso.V1>();
        services.AddScoped<SamlSigninStateRepository>();
        services.AddSingleton<ISamlSigninStateSerializer, JsonSamlSigninStateSerializer>();
        services.AddScoped<ISamlSigninStateStore, SamlSigninStateStore>();

        _provider = services.BuildServiceProvider();

        var schema = _provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);
    }

    public override async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override StoreHandle<ISamlSigninStateStore> CreateStore()
    {
        var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISamlSigninStateStore>();
        return StoreHandle.WithDisposable(store, scope);
    }
}
