// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class PushedAuthorizationStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IPushedAuthorizationRequestStore BuildStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IPushedAuthorizationRequestStore>();
    }

    [Fact]
    public async Task store_then_get_by_hash_returns_request()
    {
        var store = BuildStore();

        var par = CreatePar();

        await store.StoreAsync(par, _ct);

        var result = await store.GetByHashAsync(par.ReferenceValueHash, _ct);

        result.ShouldNotBeNull();
        result.ReferenceValueHash.ShouldBe(par.ReferenceValueHash);
        result.Parameters.ShouldBe(par.Parameters);
        result.ExpiresAtUtc.ShouldBe(par.ExpiresAtUtc);
    }

    [Fact]
    public async Task get_by_hash_nonexistent_returns_null()
    {
        var store = BuildStore();

        var result = await store.GetByHashAsync(GenerateHash(), _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task consume_by_hash_removes_request()
    {
        var store = BuildStore();

        var par = CreatePar();

        await store.StoreAsync(par, _ct);

        await store.ConsumeByHashAsync(par.ReferenceValueHash, _ct);

        var result = await store.GetByHashAsync(par.ReferenceValueHash, _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task consume_nonexistent_does_not_throw()
    {
        var store = BuildStore();

        // Should be idempotent — no exception
        await store.ConsumeByHashAsync(GenerateHash(), _ct);
    }

    [Fact]
    public async Task store_duplicate_hash_throws()
    {
        var store = BuildStore();

        var par = CreatePar();

        await store.StoreAsync(par, _ct);

        // Same hash, different parameters
        var duplicate = new PushedAuthorizationRequest
        {
            ReferenceValueHash = par.ReferenceValueHash,
            Parameters = "different-parameters",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };

        await Should.ThrowAsync<InvalidOperationException>(
            () => store.StoreAsync(duplicate, _ct));
    }

    [Fact]
    public async Task store_preserves_expiration_time()
    {
        var store = BuildStore();

        var expiresAt = new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var par = new PushedAuthorizationRequest
        {
            ReferenceValueHash = GenerateHash(),
            Parameters = "test-params",
            ExpiresAtUtc = expiresAt
        };

        await store.StoreAsync(par, _ct);

        var result = await store.GetByHashAsync(par.ReferenceValueHash, _ct);

        result.ShouldNotBeNull();
        result.ExpiresAtUtc.ShouldBe(expiresAt);
    }

    [Fact]
    public async Task multiple_pars_are_independent()
    {
        var store = BuildStore();

        var par1 = CreatePar();
        var par2 = CreatePar();

        await store.StoreAsync(par1, _ct);
        await store.StoreAsync(par2, _ct);

        // Consume one, the other should remain
        await store.ConsumeByHashAsync(par1.ReferenceValueHash, _ct);

        var result1 = await store.GetByHashAsync(par1.ReferenceValueHash, _ct);
        var result2 = await store.GetByHashAsync(par2.ReferenceValueHash, _ct);

        result1.ShouldBeNull();
        result2.ShouldNotBeNull();
        result2.Parameters.ShouldBe(par2.Parameters);
    }

    private static string GenerateHash() => Guid.NewGuid().ToString("N");

    private static PushedAuthorizationRequest CreatePar() => new()
    {
        ReferenceValueHash = GenerateHash(),
        Parameters = $"client_id=test&scope=openid&nonce={Guid.NewGuid():N}",
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
    };

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
