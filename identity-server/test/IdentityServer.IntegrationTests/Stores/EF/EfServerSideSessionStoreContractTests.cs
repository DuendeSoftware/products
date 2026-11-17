// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.EntityFramework;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    [Fact]
    public async Task UpdateSessionAsync_WhenTheSessionIsSavedBetweenTheVersionCompareAndTheWrite_IsRefusedAndTheSameContextStillStoresAGrant()
    {
        var aliceSession = CreateTestSession(expires: DateTime.UtcNow.AddHours(1));
        await using (var signInHandle = CreateStore())
        {
            await signInHandle.Store.CreateSessionAsync(aliceSession, _ct);
        }

        var browserSavesTheSessionFirst = new BrowserSavesTheSessionDuringTheNextWrite(_options, aliceSession.Key);
        await using var tokenCallContext = new PersistedGrantDbContext(
            new DbContextOptionsBuilder<PersistedGrantDbContext>(_options).AddInterceptors(browserSavesTheSessionFirst).Options);
        var tokenCallSessionStore = new ServerSideSessionStore(tokenCallContext, new NullLogger<ServerSideSessionStore>(), TimeProvider.System);
        var tokenCallGrantStore = new PersistedGrantStore(tokenCallContext, new NullLogger<PersistedGrantStore>());
        var tokenCallSessionRead = await tokenCallSessionStore.GetSessionAsync(aliceSession.Key, _ct);
        var refreshTokenGrant = new Duende.IdentityServer.Models.PersistedGrant
        {
            Key = $"refresh_{Guid.NewGuid():N}",
            Type = "refresh_token",
            ClientId = "client",
            SubjectId = aliceSession.SubjectId,
            SessionId = aliceSession.SessionId,
            CreationTime = DateTime.UtcNow,
            Expiration = DateTime.UtcNow.AddHours(1),
            Data = "refresh_token_data"
        };

        var tokenCallSaveAccepted = await tokenCallSessionStore.UpdateSessionAsync(tokenCallSessionRead!, _ct);
        await tokenCallGrantStore.StoreAsync(refreshTokenGrant, _ct);

        tokenCallSaveAccepted.ShouldBeFalse("a save whose version check fails in the database must be refused");
        await using var verificationContext = new PersistedGrantDbContext(_options);
        var storedGrant = await verificationContext.PersistedGrants.SingleOrDefaultAsync(x => x.Key == refreshTokenGrant.Key, _ct);
        storedGrant.ShouldNotBeNull("a refused session save must not stop the same request from storing its refresh token");
    }

    private sealed class BrowserSavesTheSessionDuringTheNextWrite(DbContextOptions<PersistedGrantDbContext> options, string sessionKey) : SaveChangesInterceptor
    {
        private int _browserSaves;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, Ct cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _browserSaves, 1) == 0)
            {
                await using var browserContext = new PersistedGrantDbContext(options);
                await browserContext.ServerSideSessions
                    .Where(x => x.Key == sessionKey)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
            }

            return result;
        }
    }
}
