// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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

    /// <summary>
    /// Deletes all PersistedGrant rows via the same connection right before
    /// SaveChanges executes, simulating a concurrent deletion.
    /// </summary>
    private sealed class DeleteBeforeSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
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
