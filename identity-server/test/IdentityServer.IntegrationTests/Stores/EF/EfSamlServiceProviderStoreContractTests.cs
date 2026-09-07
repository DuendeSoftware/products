// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

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

public class EfSamlServiceProviderStoreContractTests : SamlServiceProviderStoreContractTests
{
    private readonly DbContextOptions<ConfigurationDbContext> _options;

    public EfSamlServiceProviderStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            $"EfSamlServiceProviderContract_{Guid.NewGuid():N}", new ConfigurationStoreOptions());
        using var context = new ConfigurationDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<ISamlServiceProviderStore> CreateStore()
    {
        var context = new ConfigurationDbContext(_options);
        ISamlServiceProviderStore store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    protected override async Task<StoreHandle<ISamlServiceProviderStore>> CreateIsolatedStoreAsync()
    {
        var freshOptions = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            $"EfSamlServiceProviderContractIsolated_{Guid.NewGuid():N}", new ConfigurationStoreOptions());
        var context = new ConfigurationDbContext(freshOptions);
        await context.Database.EnsureCreatedAsync(_ct);
        ISamlServiceProviderStore store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    protected override async Task SeedSamlServiceProviderAsync(SamlServiceProvider sp)
    {
        await using var context = new ConfigurationDbContext(_options);
        context.SamlServiceProviders.Add(sp.ToEntity());
        await context.SaveChangesAsync(_ct);
    }
}
