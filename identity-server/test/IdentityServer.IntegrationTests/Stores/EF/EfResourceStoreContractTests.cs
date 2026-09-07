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

public class EfResourceStoreContractTests : ResourceStoreContractTests
{
    private readonly DbContextOptions<ConfigurationDbContext> _options;

    public EfResourceStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            $"EfResourceContract_{Guid.NewGuid():N}", new ConfigurationStoreOptions());
        using var context = new ConfigurationDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<IResourceStore> CreateStore()
    {
        var context = new ConfigurationDbContext(_options);
        IResourceStore store = new ResourceStore(context, new NullLogger<ResourceStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    protected override async Task SeedIdentityResourceAsync(IdentityResource resource)
    {
        await using var context = new ConfigurationDbContext(_options);
        context.IdentityResources.Add(resource.ToEntity());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    protected override async Task SeedApiResourceAsync(ApiResource resource)
    {
        await using var context = new ConfigurationDbContext(_options);
        context.ApiResources.Add(resource.ToEntity());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    protected override async Task SeedApiScopeAsync(ApiScope scope)
    {
        await using var context = new ConfigurationDbContext(_options);
        context.ApiScopes.Add(scope.ToEntity());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
