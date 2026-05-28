// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.Storage.Stores;

public class SamlLogoutSessionStoreTests : IntegrationTest<SamlLogoutSessionStoreTests, PersistedGrantDbContext, OperationalStoreOptions>
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public SamlLogoutSessionStoreTests(DatabaseProviderFixture<PersistedGrantDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new PersistedGrantDbContext(options);
            context.Database.EnsureCreated();
        }
    }

    private static SamlLogoutSession CreateSession(string? logoutId = null, string? requestIdPrefix = null)
    {
        var prefix = requestIdPrefix ?? Guid.NewGuid().ToString("N")[..8];
        return new()
        {
            LogoutId = logoutId ?? Guid.NewGuid().ToString("N"),
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                [$"_req-{prefix}-sp1"] = new("https://sp1.example.com"),
                [$"_req-{prefix}-sp2"] = new("https://sp2.example.com"),
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };
    }

    private static SamlLogoutSessionStore CreateStore(
        PersistedGrantDbContext context,
        TimeProvider? timeProvider = null,
        TimeSpan? sessionLifetime = null) =>
        new(
            context,
            timeProvider ?? TimeProvider.System,
            NullLogger<SamlLogoutSessionStore>.Instance);

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreAsync_WhenSuccessful_ExpectSessionRetrievable(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var retrieved = await CreateStore(context).GetByLogoutIdAsync(session.LogoutId, _ct);

            retrieved.ShouldNotBeNull();
            retrieved.LogoutId.ShouldBe(session.LogoutId);
            retrieved.ExpectedResponses.Count.ShouldBe(2);
            retrieved.ExpectedResponses.Keys.ShouldAllBe(k => k.Contains("-sp1") || k.Contains("-sp2"));
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetByLogoutIdAsync_WhenSessionDoesNotExist_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using var context = new PersistedGrantDbContext(options);
        var result = await CreateStore(context).GetByLogoutIdAsync("nonexistent", _ct);
        result.ShouldBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetByLogoutIdAsync_WhenSessionExpired_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        // Manually expire the entity
        await using (var context = new PersistedGrantDbContext(options))
        {
            var entity = await context.SamlLogoutSessions.SingleAsync(x => x.LogoutId == session.LogoutId, _ct);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).GetByLogoutIdAsync(session.LogoutId, _ct);
            result.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task TryRecordResponseAsync_WhenRequestIdExists_ExpectTrue(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", true, _ct);
            result.ShouldBeTrue();
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var retrieved = await CreateStore(context).GetByLogoutIdAsync(session.LogoutId, _ct);
            retrieved.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response!.Success.ShouldBeTrue();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task TryRecordResponseAsync_WhenRequestIdDoesNotExist_ExpectFalse(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).TryRecordResponseAsync("_nonexistent", "https://sp1.example.com", true, _ct);
            result.ShouldBeFalse();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task TryRecordResponseAsync_WhenIssuerMismatch_ExpectFalse(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).TryRecordResponseAsync(sp1RequestId, "https://wrong-issuer.example.com", true, _ct);
            result.ShouldBeFalse();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveAsync_WhenSessionExists_ExpectSessionDeleted(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).RemoveAsync(session.LogoutId, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).GetByLogoutIdAsync(session.LogoutId, _ct);
            result.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveAsync_WhenSessionDoesNotExist_ExpectNoException(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using var context = new PersistedGrantDbContext(options);

        // Should not throw even if session doesn't exist
        await CreateStore(context).RemoveAsync("nonexistent", _ct);
        await CreateStore(context).RemoveAsync("nonexistent", _ct);
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreAsync_WhenCustomExpirySet_ExpectExpiryPersistedFromModel(DbContextOptions<PersistedGrantDbContext> options)
    {
        var customExpiry = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var session = CreateSession();
        // Use reflection-free approach: create a new session with the custom expiry
        var customSession = new SamlLogoutSession
        {
            LogoutId = session.LogoutId,
            ExpectedResponses = session.ExpectedResponses,
            CreatedUtc = session.CreatedUtc,
            ExpiresAtUtc = customExpiry,
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(customSession, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var entity = await context.SamlLogoutSessions.SingleAsync(x => x.LogoutId == session.LogoutId, _ct);
            entity.ExpiresAtUtc.ShouldBe(customExpiry);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task TryRecordResponseAsync_WhenSessionExpired_ExpectFalse(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        // Manually expire the entity
        await using (var context = new PersistedGrantDbContext(options))
        {
            var entity = await context.SamlLogoutSessions.SingleAsync(x => x.LogoutId == session.LogoutId, _ct);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", true, _ct);
            result.ShouldBeFalse();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task TryRecordResponseAsync_WhenSuccessFalse_ExpectResponseRecorded(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", false, _ct);
            result.ShouldBeTrue();
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var retrieved = await CreateStore(context).GetByLogoutIdAsync(session.LogoutId, _ct);
            retrieved.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response!.Success.ShouldBeFalse();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreAsync_WhenDuplicateLogoutId_ExpectException(DbContextOptions<PersistedGrantDbContext> options)
    {
        var logoutId = Guid.NewGuid().ToString("N");
        var session1 = CreateSession(logoutId: logoutId);
        var session2 = CreateSession(logoutId: logoutId);

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session1, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            await Should.ThrowAsync<DbUpdateException>(
                async () => await CreateStore(context).StoreAsync(session2, _ct));
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveAsync_WhenSessionExists_ExpectRequestIndicesAlsoDeleted(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        // Verify index rows exist before removal
        await using (var context = new PersistedGrantDbContext(options))
        {
            var indexCount = await context.SamlLogoutSessionRequestIndices.CountAsync(_ct);
            indexCount.ShouldBeGreaterThan(0);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).RemoveAsync(session.LogoutId, _ct);
        }

        // Verify cascade delete removed index rows
        await using (var context = new PersistedGrantDbContext(options))
        {
            var indexCount = await context.SamlLogoutSessionRequestIndices
                .Where(x => session.ExpectedResponses.Keys.Contains(x.RequestId))
                .CountAsync(_ct);
            indexCount.ShouldBe(0);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task TryRecordResponseAsync_WhenConcurrentUpdate_ExpectRetrySucceeds(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateSession();
        var requestIds = session.ExpectedResponses.Keys.ToList();
        var sp1RequestId = requestIds.First(k => k.Contains("-sp1"));
        var sp2RequestId = requestIds.First(k => k.Contains("-sp2"));

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).StoreAsync(session, _ct);
        }

        // Simulate concurrent updates by recording responses from two separate contexts.
        // Both should succeed because the retry mechanism handles concurrency conflicts.
        bool result1;
        bool result2;

        await using (var context1 = new PersistedGrantDbContext(options))
        await using (var context2 = new PersistedGrantDbContext(options))
        {
            // Start both operations — one will conflict and retry
            var task1 = CreateStore(context1).TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", true, _ct);
            var task2 = CreateStore(context2).TryRecordResponseAsync(sp2RequestId, "https://sp2.example.com", true, _ct);

            var results = await Task.WhenAll(task1, task2);
            result1 = results[0];
            result2 = results[1];
        }

        result1.ShouldBeTrue();
        result2.ShouldBeTrue();

        // Verify both responses were recorded
        await using (var context = new PersistedGrantDbContext(options))
        {
            var retrieved = await CreateStore(context).GetByLogoutIdAsync(session.LogoutId, _ct);
            retrieved.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp2RequestId].Response.ShouldNotBeNull();
        }
    }
}
