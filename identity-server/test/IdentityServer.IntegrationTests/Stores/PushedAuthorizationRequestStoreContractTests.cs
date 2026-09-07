// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for IPushedAuthorizationRequestStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class PushedAuthorizationRequestStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<IPushedAuthorizationRequestStore> CreateStore();

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static PushedAuthorizationRequest CreateTestPar(
        string? referenceValueHash = null,
        DateTime? expiresAtUtc = null,
        string? parameters = null) =>
        new()
        {
            ReferenceValueHash = referenceValueHash ?? Guid.NewGuid().ToString("N"),
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddHours(1),
            Parameters = parameters ?? $"client_id=test&redirect_uri=https://example.com&nonce={Guid.NewGuid():N}"
        };

    [Fact]
    public async Task StoreAsync_ThenGetByHash_ReturnsRequest()
    {
        await using var handle = CreateStore();
        var par = CreateTestPar();

        await handle.Store.StoreAsync(par, _ct);

        var result = await handle.Store.GetByHashAsync(par.ReferenceValueHash, _ct);

        result.ShouldNotBeNull();
        result.ReferenceValueHash.ShouldBe(par.ReferenceValueHash);
        result.Parameters.ShouldBe(par.Parameters);
        result.ExpiresAtUtc.ShouldBeCloseTo(par.ExpiresAtUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetByHashAsync_WhenNotExists_ReturnsNull()
    {
        await using var handle = CreateStore();

        var result = await handle.Store.GetByHashAsync(Guid.NewGuid().ToString("N"), _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ConsumeByHashAsync_ThenGetByHash_ReturnsNull()
    {
        await using var handle = CreateStore();
        var par = CreateTestPar();
        await handle.Store.StoreAsync(par, _ct);

        // Verify the record exists before consuming
        var before = await handle.Store.GetByHashAsync(par.ReferenceValueHash, _ct);
        before.ShouldNotBeNull();

        await handle.Store.ConsumeByHashAsync(par.ReferenceValueHash, _ct);

        var result = await handle.Store.GetByHashAsync(par.ReferenceValueHash, _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public virtual async Task GetByHashAsync_WhenExpired_ReturnsNull()
    {
        await using var handle = CreateStore();
        var par = CreateTestPar(expiresAtUtc: DateTime.UtcNow.AddDays(-1));
        await handle.Store.StoreAsync(par, _ct);

        var result = await handle.Store.GetByHashAsync(par.ReferenceValueHash, _ct);

        // Implementations that enforce TTL at the store level (e.g., Storage) will return null.
        // Implementations that do not (e.g., EF, InMemory) may return the expired request;
        // those implementations override this test.
        result.ShouldBeNull();
    }
}
