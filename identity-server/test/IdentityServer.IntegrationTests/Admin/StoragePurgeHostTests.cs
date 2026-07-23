// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Integration tests for StoragePurgeHost — verifies that expired entities are actually
/// removed from the store when RunPurgeAsync is called.
/// </summary>
public sealed class StoragePurgeHostTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();
    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task run_purge_removes_expired_persisted_grants()
    {
        using var scope = _fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
        var purgeHost = CreatePurgeHost(scope);

        // Store a grant that is already expired
        var expiredGrant = new PersistedGrant
        {
            Key = $"expired_{Guid.NewGuid():N}",
            SubjectId = "sub_1",
            ClientId = "client_1",
            Type = "authorization_code",
            Description = "test",
            CreationTime = DateTime.UtcNow.AddHours(-2),
            Expiration = DateTime.UtcNow.AddHours(-1),
            Data = "{\"test\":true}"
        };
        await store.StoreAsync(expiredGrant, _ct);

        // Store a grant that is NOT expired
        var validGrant = new PersistedGrant
        {
            Key = $"valid_{Guid.NewGuid():N}",
            SubjectId = "sub_2",
            ClientId = "client_2",
            Type = "authorization_code",
            Description = "test",
            CreationTime = DateTime.UtcNow,
            Expiration = DateTime.UtcNow.AddHours(1),
            Data = "{\"test\":true}"
        };
        await store.StoreAsync(validGrant, _ct);

        // Run purge
        await purgeHost.RunPurgeAsync(_ct);

        // Expired grant should be gone
        var fetched = await store.GetAsync(expiredGrant.Key, _ct);
        fetched.ShouldBeNull();

        // Valid grant should remain
        var remaining = await store.GetAsync(validGrant.Key, _ct);
        remaining.ShouldNotBeNull();
    }

    [Fact]
    public async Task run_purge_handles_multiple_batches()
    {
        using var scope = _fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
        var options = scope.ServiceProvider.GetRequiredService<IdentityServerOptions>();
        options.StoragePurge.BatchSize = 5;
        var purgeHost = CreatePurgeHost(scope);

        // Seed more expired grants than the batch size
        for (var i = 0; i < 12; i++)
        {
            await store.StoreAsync(new PersistedGrant
            {
                Key = $"batch_{i}_{Guid.NewGuid():N}",
                SubjectId = "sub_batch",
                ClientId = "client_batch",
                Type = "refresh_token",
                Description = "batch test",
                CreationTime = DateTime.UtcNow.AddHours(-2),
                Expiration = DateTime.UtcNow.AddHours(-1),
                Data = "{}"
            }, _ct);
        }

        // Run purge — should loop multiple times (12 records / 5 batch = 3 iterations)
        await purgeHost.RunPurgeAsync(_ct);

        // All expired grants should be gone
        var remaining = await store.GetAllAsync(
            new PersistedGrantFilter { SubjectId = "sub_batch" }, _ct);
        remaining.Count.ShouldBe(0);
    }

    private static StoragePurgeHost CreatePurgeHost(IServiceScope scope)
    {
        var storeFactory = scope.ServiceProvider.GetRequiredService<IStoreFactory>();
        var options = scope.ServiceProvider.GetRequiredService<IdentityServerOptions>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<StoragePurgeHost>>();
        return new StoragePurgeHost(storeFactory, options, logger);
    }
}
