// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework;

public class EntityFrameworkBasedSessionCleanupConcurrencyTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RemoveAllAsync_WhenConcurrentDeletionCausesConcurrencyException_DetachesEntries()
    {
        var interceptor = new DeleteBeforeSaveInterceptor();
        var options = BuildSqliteOptions(interceptor);

        await using (var setupCtx = new PersistedGrantDbContext(options))
        {
            await setupCtx.Database.EnsureCreatedAsync(_ct);
            setupCtx.PersistedGrants.Add(new PersistedGrant
            {
                Key = "grant-1",
                Type = "reference_token",
                ClientId = "client",
                SubjectId = "sub",
                SessionId = "sid",
                CreationTime = DateTime.UtcNow,
                Data = "{}"
            });
            await setupCtx.SaveChangesAsync(_ct);
        }

        interceptor.Enabled = true;

        await using var storeCtx = new PersistedGrantDbContext(options);
        var store = new PersistedGrantStore(storeCtx, new NullLogger<PersistedGrantStore>());

        await store.RemoveAllAsync(new IdentityServer.Stores.PersistedGrantFilter
        {
            SubjectId = "sub",
            SessionId = "sid"
        }, _ct);

        var leakedEntries = storeCtx.ChangeTracker.Entries<PersistedGrant>()
            .Where(e => e.State != EntityState.Detached)
            .ToList();
        leakedEntries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task GetAndRemoveExpiredSessionsAsync_WhenSessionsAreRenewedDuringCleanup_ExpectOnlyDeletedSessionsReturned(int renewedCount)
    {
        var expiredKeys = Enumerable.Range(0, 6).Select(i => $"session-{i}").ToArray();
        var renewedKeys = expiredKeys.Take(renewedCount).ToArray();
        var interceptor = new RenewSessionsAfterTheyWereSelectedInterceptor(renewedKeys);
        var options = BuildSqliteOptions(interceptor);
        await using (var setupCtx = new PersistedGrantDbContext(options))
        {
            await setupCtx.Database.EnsureCreatedAsync(_ct);
            var setupStore = new ServerSideSessionStore(setupCtx, NullLogger<ServerSideSessionStore>.Instance, TimeProvider.System);
            foreach (var key in expiredKeys)
            {
                await setupStore.CreateSessionAsync(new IdentityServer.Models.ServerSideSession
                {
                    Key = key,
                    Scheme = "cookie",
                    SubjectId = $"sub-{key}",
                    SessionId = $"sid-{key}",
                    Created = DateTime.UtcNow.AddDays(-2),
                    Renewed = DateTime.UtcNow.AddDays(-2),
                    Expires = DateTime.UtcNow.AddDays(-1),
                    Ticket = "ticket"
                }, _ct);
            }
        }

        interceptor.Options = options;
        await using var cleanupCtx = new PersistedGrantDbContext(options);
        var cleanupStore = new ServerSideSessionStore(cleanupCtx, NullLogger<ServerSideSessionStore>.Instance, TimeProvider.System);

        var sessionsReportedAsRemoved = await cleanupStore.GetAndRemoveExpiredSessionsAsync(100, _ct);

        await using var verifyCtx = new PersistedGrantDbContext(options);
        var remainingKeys = await verifyCtx.ServerSideSessions.Select(x => x.Key).ToArrayAsync(_ct);
        sessionsReportedAsRemoved.Select(x => x.Key).ShouldBe(expiredKeys.Except(renewedKeys), ignoreOrder: true);
        remainingKeys.ShouldBe(renewedKeys, ignoreOrder: true);
    }

    private static DbContextOptions<PersistedGrantDbContext> BuildSqliteOptions(IInterceptor interceptor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new OperationalStoreOptions());

        var connection = new SqliteConnection($"DataSource={Guid.NewGuid()};Mode=Memory;");
        connection.Open();

        return new DbContextOptionsBuilder<PersistedGrantDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .UseApplicationServiceProvider(services.BuildServiceProvider())
            .Options;
    }

    private sealed class RenewSessionsAfterTheyWereSelectedInterceptor(string[] keys) : DbCommandInterceptor
    {
        private bool _renewed;

        public DbContextOptions<PersistedGrantDbContext> Options { get; set; } = null!;

        public override async ValueTask<InterceptionResult> DataReaderClosingAsync(
            DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
        {
            if (!_renewed && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal))
            {
                _renewed = true;
                await using var renewingCtx = new PersistedGrantDbContext(Options);
                await renewingCtx.ServerSideSessions.Where(x => keys.Contains(x.Key))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Expires, DateTime.UtcNow.AddHours(1))
                        .SetProperty(x => x.Version, x => x.Version + 1));
            }

            return await base.DataReaderClosingAsync(command, eventData, result);
        }
    }

    /// <summary>
    /// Deletes all PersistedGrant rows via the same connection right before
    /// SaveChanges executes, simulating a concurrent deletion.
    /// </summary>
    private sealed class DeleteBeforeSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, Ct ct = default)
        {
            if (Enabled && eventData.Context is not null)
            {
                Enabled = false;
                await eventData.Context.Database.ExecuteSqlRawAsync("DELETE FROM PersistedGrants", ct);
            }

            return await base.SavingChangesAsync(eventData, result, ct);
        }
    }
}
