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

public class EfIdentityProviderStoreContractTests : IdentityProviderStoreContractTests
{
    private readonly DbContextOptions<ConfigurationDbContext> _options;

    public EfIdentityProviderStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            $"EfIdentityProviderContract_{Guid.NewGuid():N}", new ConfigurationStoreOptions());
        using var context = new ConfigurationDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<IIdentityProviderStore> CreateStore()
    {
        var context = new ConfigurationDbContext(_options);
        IIdentityProviderStore store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>(), new TestIdentityProviderFactory());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    protected override async Task SeedIdentityProviderAsync(IdentityProvider idp)
    {
        await using var context = new ConfigurationDbContext(_options);
        context.IdentityProviders.Add(idp.ToEntity());
        await context.SaveChangesAsync(_ct);
    }
}
