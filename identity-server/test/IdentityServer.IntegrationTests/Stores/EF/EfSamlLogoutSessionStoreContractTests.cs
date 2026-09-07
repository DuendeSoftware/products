// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.EntityFramework;
using Duende.IdentityServer.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Stores.EF;

public class EfSamlLogoutSessionStoreContractTests : SamlLogoutSessionStoreContractTests
{
    private readonly DbContextOptions<PersistedGrantDbContext> _options;

    public EfSamlLogoutSessionStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>(
            $"EfSamlLogoutSessionContract_{Guid.NewGuid():N}", new OperationalStoreOptions());
        using var context = new PersistedGrantDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<ISamlLogoutSessionStore> CreateStore()
    {
        var context = new PersistedGrantDbContext(_options);
        ISamlLogoutSessionStore store = new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance);
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    // --- EF-specific tests ---

    [Fact]
    public async Task GetByLogoutIdAsync_WhenSessionExpired_ExpectNull()
    {
        var session = CreateSession();

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .StoreAsync(session, _ct);
        }

        // Manually expire the entity
        await using (var context = new PersistedGrantDbContext(_options))
        {
            var entity = await context.SamlLogoutSessions.SingleAsync(x => x.LogoutId == session.LogoutId, _ct);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var result = await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .GetByLogoutIdAsync(session.LogoutId, _ct);
            result.ShouldBeNull();
        }
    }

    [Fact]
    public async Task StoreAsync_WhenDuplicateLogoutId_ExpectException()
    {
        var logoutId = Guid.NewGuid().ToString("N");
        var session1 = CreateSession(logoutId: logoutId);
        var session2 = CreateSession(logoutId: logoutId);

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .StoreAsync(session1, _ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await Should.ThrowAsync<DbUpdateException>(
                async () => await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                    .StoreAsync(session2, _ct));
        }
    }

    [Fact]
    public async Task RemoveAsync_WhenSessionExists_ExpectRequestIndicesAlsoDeleted()
    {
        var session = CreateSession();

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .StoreAsync(session, _ct);
        }

        // Verify index rows exist before removal
        await using (var context = new PersistedGrantDbContext(_options))
        {
            var indexCount = await context.SamlLogoutSessionRequestIndices.CountAsync(_ct);
            indexCount.ShouldBeGreaterThan(0);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .RemoveAsync(session.LogoutId, _ct);
        }

        // Verify cascade delete removed index rows
        await using (var context = new PersistedGrantDbContext(_options))
        {
            var indexCount = await context.SamlLogoutSessionRequestIndices
                .Where(x => session.ExpectedResponses.Keys.Contains(x.RequestId))
                .CountAsync(_ct);
            indexCount.ShouldBe(0);
        }
    }

    [Fact]
    public async Task StoreAsync_WhenCustomExpirySet_ExpectExpiryPersistedFromModel()
    {
        var customExpiry = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var session = CreateSession();
        var customSession = new SamlLogoutSession
        {
            LogoutId = session.LogoutId,
            ExpectedResponses = session.ExpectedResponses,
            CreatedUtc = session.CreatedUtc,
            ExpiresAtUtc = customExpiry,
        };

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .StoreAsync(customSession, _ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var entity = await context.SamlLogoutSessions.SingleAsync(x => x.LogoutId == session.LogoutId, _ct);
            entity.ExpiresAtUtc.ShouldBe(customExpiry);
        }
    }

    [Fact]
    public async Task TryRecordResponseAsync_WhenSessionExpired_ExpectFalse()
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .StoreAsync(session, _ct);
        }

        // Manually expire the entity
        await using (var context = new PersistedGrantDbContext(_options))
        {
            var entity = await context.SamlLogoutSessions.SingleAsync(x => x.LogoutId == session.LogoutId, _ct);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var result = await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", true, _ct);
            result.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task TryRecordResponseAsync_WhenConcurrentUpdate_ExpectRetrySucceeds()
    {
        var session = CreateSession();
        var requestIds = session.ExpectedResponses.Keys.ToList();
        var sp1RequestId = requestIds.First(k => k.Contains("-sp1"));
        var sp2RequestId = requestIds.First(k => k.Contains("-sp2"));

        await using (var context = new PersistedGrantDbContext(_options))
        {
            await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .StoreAsync(session, _ct);
        }

        bool result1;
        bool result2;

        await using (var context1 = new PersistedGrantDbContext(_options))
        await using (var context2 = new PersistedGrantDbContext(_options))
        {
            var task1 = new SamlLogoutSessionStore(context1, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", true, _ct);
            var task2 = new SamlLogoutSessionStore(context2, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .TryRecordResponseAsync(sp2RequestId, "https://sp2.example.com", true, _ct);

            var results = await Task.WhenAll(task1, task2);
            result1 = results[0];
            result2 = results[1];
        }

        result1.ShouldBeTrue();
        result2.ShouldBeTrue();

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var retrieved = await new SamlLogoutSessionStore(context, TimeProvider.System, NullLogger<SamlLogoutSessionStore>.Instance)
                .GetByLogoutIdAsync(session.LogoutId, _ct);
            retrieved.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp2RequestId].Response.ShouldNotBeNull();
        }
    }
}
