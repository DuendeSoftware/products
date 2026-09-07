// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.PushedAuthorization;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StoragePushedAuthorizationRequestStoreContractTests : PushedAuthorizationRequestStoreContractTests
{
    private readonly string _dbName = $"StoragePushedAuthorizationContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<PushedAuthorizationDso.V1>();
        services.AddScoped<PushedAuthorizationRepository>();
        services.AddScoped<IPushedAuthorizationRequestStore, PushedAuthorizationStore>();

        _provider = services.BuildServiceProvider();

        var schema = _provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override StoreHandle<IPushedAuthorizationRequestStore> CreateStore()
    {
        var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPushedAuthorizationRequestStore>();
        return StoreHandle.WithDisposable(store, scope);
    }
}
