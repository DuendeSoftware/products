// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for ISamlLogoutSessionStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class SamlLogoutSessionStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<ISamlLogoutSessionStore> CreateStore();

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static SamlLogoutSession CreateSession(string? logoutId = null, string? requestIdPrefix = null)
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

    [Fact]
    public async Task StoreAsync_WhenSuccessful_ExpectSessionRetrievable()
    {
        var session = CreateSession();

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreAsync(session, _ct);
        }

        await using (var handle = CreateStore())
        {
            var retrieved = await handle.Store.GetByLogoutIdAsync(session.LogoutId, _ct);

            retrieved.ShouldNotBeNull();
            retrieved.LogoutId.ShouldBe(session.LogoutId);
            retrieved.ExpectedResponses.Count.ShouldBe(2);
            retrieved.ExpectedResponses.Keys.ShouldAllBe(k => k.Contains("-sp1") || k.Contains("-sp2"));
        }
    }

    [Fact]
    public async Task GetByLogoutIdAsync_WhenSessionDoesNotExist_ExpectNull()
    {
        await using var handle = CreateStore();
        var result = await handle.Store.GetByLogoutIdAsync("nonexistent", _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryRecordResponseAsync_WhenRequestIdExists_ExpectTrue()
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreAsync(session, _ct);
        }

        await using (var handle = CreateStore())
        {
            var result = await handle.Store.TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", true, _ct);
            result.ShouldBeTrue();
        }

        await using (var handle = CreateStore())
        {
            var retrieved = await handle.Store.GetByLogoutIdAsync(session.LogoutId, _ct);
            retrieved.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response!.Success.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task TryRecordResponseAsync_WhenRequestIdDoesNotExist_ExpectFalse()
    {
        var session = CreateSession();

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreAsync(session, _ct);
        }

        await using (var handle = CreateStore())
        {
            var result = await handle.Store.TryRecordResponseAsync("_nonexistent", "https://sp1.example.com", true, _ct);
            result.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task TryRecordResponseAsync_WhenIssuerMismatch_ExpectFalse()
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreAsync(session, _ct);
        }

        await using (var handle = CreateStore())
        {
            var result = await handle.Store.TryRecordResponseAsync(sp1RequestId, "https://wrong-issuer.example.com", true, _ct);
            result.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task TryRecordResponseAsync_WhenSuccessFalse_ExpectResponseRecorded()
    {
        var session = CreateSession();
        var sp1RequestId = session.ExpectedResponses.Keys.First(k => k.Contains("-sp1"));

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreAsync(session, _ct);
        }

        await using (var handle = CreateStore())
        {
            var result = await handle.Store.TryRecordResponseAsync(sp1RequestId, "https://sp1.example.com", false, _ct);
            result.ShouldBeTrue();
        }

        await using (var handle = CreateStore())
        {
            var retrieved = await handle.Store.GetByLogoutIdAsync(session.LogoutId, _ct);
            retrieved.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response.ShouldNotBeNull();
            retrieved.ExpectedResponses[sp1RequestId].Response!.Success.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task RemoveAsync_WhenSessionExists_ExpectSessionDeleted()
    {
        var session = CreateSession();

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreAsync(session, _ct);
        }

        await using (var handle = CreateStore())
        {
            await handle.Store.RemoveAsync(session.LogoutId, _ct);
        }

        await using (var handle = CreateStore())
        {
            var result = await handle.Store.GetByLogoutIdAsync(session.LogoutId, _ct);
            result.ShouldBeNull();
        }
    }

    [Fact]
    public async Task RemoveAsync_WhenSessionDoesNotExist_ExpectNoException()
    {
        await using var handle = CreateStore();

        // Should not throw even if session doesn't exist
        await handle.Store.RemoveAsync("nonexistent", _ct);
        await handle.Store.RemoveAsync("nonexistent", _ct);
    }
}
