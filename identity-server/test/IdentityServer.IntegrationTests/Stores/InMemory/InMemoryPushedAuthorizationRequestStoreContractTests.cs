// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for IPushedAuthorizationRequestStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing store
/// starts empty for each test — providing natural test isolation.
/// </summary>
public class InMemoryPushedAuthorizationRequestStoreContractTests : PushedAuthorizationRequestStoreContractTests
{
    protected override StoreHandle<IPushedAuthorizationRequestStore> CreateStore() =>
        new(new InMemoryPushedAuthorizationRequestStore());

    /// <summary>
    /// InMemory does not enforce TTL at the store level; expired PARs are still returned.
    /// </summary>
    [Fact]
    public override async Task GetByHashAsync_WhenExpired_ReturnsNull()
    {
        await using var handle = CreateStore();
        var par = CreateTestPar(expiresAtUtc: DateTime.UtcNow.AddDays(-1));
        await handle.Store.StoreAsync(par, _ct);

        var result = await handle.Store.GetByHashAsync(par.ReferenceValueHash, _ct);

        // InMemory does not filter by expiration — the expired PAR is still returned.
        result.ShouldNotBeNull();
        result.ReferenceValueHash.ShouldBe(par.ReferenceValueHash);
    }
}
