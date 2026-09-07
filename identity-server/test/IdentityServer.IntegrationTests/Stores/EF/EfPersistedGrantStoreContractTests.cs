// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.EntityFramework;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Stores.EF;

public class EfPersistedGrantStoreContractTests : PersistedGrantStoreContractTests
{
    private readonly DbContextOptions<PersistedGrantDbContext> _options;

    public EfPersistedGrantStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>(
            $"EfPersistedGrantContract_{Guid.NewGuid():N}", new OperationalStoreOptions());
        using var context = new PersistedGrantDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<IPersistedGrantStore> CreateStore()
    {
        var context = new PersistedGrantDbContext(_options);
        IPersistedGrantStore store = new PersistedGrantStore(context, new NullLogger<PersistedGrantStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    protected override async Task<StoreHandle<IPersistedGrantStore>> CreateIsolatedStoreAsync()
    {
        var freshOptions = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>(
            $"EfPersistedGrantContractIsolated_{Guid.NewGuid():N}", new OperationalStoreOptions());
        var context = new PersistedGrantDbContext(freshOptions);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        IPersistedGrantStore store = new PersistedGrantStore(context, new NullLogger<PersistedGrantStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }
}
