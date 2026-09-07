// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.EntityFramework;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Stores.EF;

public class EfPushedAuthorizationRequestStoreContractTests : PushedAuthorizationRequestStoreContractTests
{
    private readonly DbContextOptions<PersistedGrantDbContext> _options;

    public EfPushedAuthorizationRequestStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>(
            $"EfPushedAuthorizationContract_{Guid.NewGuid():N}", new OperationalStoreOptions());
        using var context = new PersistedGrantDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<IPushedAuthorizationRequestStore> CreateStore()
    {
        var context = new PersistedGrantDbContext(_options);
        IPushedAuthorizationRequestStore store = new PushedAuthorizationRequestStore(context, new NullLogger<PushedAuthorizationRequestStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    /// <summary>
    /// EF does not enforce TTL at the store level; expired PARs are still returned.
    /// </summary>
    [Fact]
    public override async Task GetByHashAsync_WhenExpired_ReturnsNull()
    {
        await using var handle = CreateStore();
        var par = CreateTestPar(expiresAtUtc: DateTime.UtcNow.AddDays(-1));
        await handle.Store.StoreAsync(par, _ct);

        var result = await handle.Store.GetByHashAsync(par.ReferenceValueHash, _ct);

        // EF does not filter by expiration — the expired PAR is still returned.
        result.ShouldNotBeNull();
        result.ReferenceValueHash.ShouldBe(par.ReferenceValueHash);
    }
}
