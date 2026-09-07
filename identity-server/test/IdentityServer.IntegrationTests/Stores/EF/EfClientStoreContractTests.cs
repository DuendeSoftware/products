// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.EntityFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Stores.EF;

public class EfClientStoreContractTests : ClientStoreContractTests
{
    private readonly DbContextOptions<ConfigurationDbContext> _options;

    public EfClientStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            $"EfClientContract_{Guid.NewGuid():N}", new ConfigurationStoreOptions());
        using var context = new ConfigurationDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<IClientStore> CreateStore()
    {
        var context = new ConfigurationDbContext(_options);
        IClientStore store = new ClientStore(context, new NullLogger<ClientStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    protected override async Task<StoreHandle<IClientStore>> CreateIsolatedStoreAsync()
    {
        var freshOptions = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            $"EfClientContractIsolated_{Guid.NewGuid():N}", new ConfigurationStoreOptions());
        var context = new ConfigurationDbContext(freshOptions);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        IClientStore store = new ClientStore(context, new NullLogger<ClientStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    protected override async Task SeedClientAsync(Client client)
    {
        await using var context = new ConfigurationDbContext(_options);
        context.Clients.Add(client.ToEntity());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    protected override async Task SeedClientsAsync(IEnumerable<Client> clients)
    {
        await using var context = new ConfigurationDbContext(_options);
        foreach (var client in clients)
        {
            context.Clients.Add(client.ToEntity());
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
