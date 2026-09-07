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

public class EfServerSideSessionStoreContractTests : ServerSideSessionStoreContractTests
{
    private readonly DbContextOptions<PersistedGrantDbContext> _options;

    public EfServerSideSessionStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>(
            $"EfServerSideSessionContract_{Guid.NewGuid():N}", new OperationalStoreOptions());
        using var context = new PersistedGrantDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<IServerSideSessionStore> CreateStore()
    {
        var context = new PersistedGrantDbContext(_options);
        IServerSideSessionStore store = new ServerSideSessionStore(context, new NullLogger<ServerSideSessionStore>(), TimeProvider.System);
        return StoreHandle.WithAsyncDisposable(store, context);
    }
}
