// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.EntityFramework;
using Duende.IdentityServer.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Stores.EF;

public class EfSamlSigninStateStoreContractTests : SamlSigninStateStoreContractTests
{
    private readonly DbContextOptions<PersistedGrantDbContext> _options;

    public EfSamlSigninStateStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>(
            $"EfSamlSigninStateContract_{Guid.NewGuid():N}", new OperationalStoreOptions());
        using var context = new PersistedGrantDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<ISamlSigninStateStore> CreateStore()
    {
        var context = new PersistedGrantDbContext(_options);
        ISamlSigninStateStore store = new SamlSigninStateStore(context, TimeProvider.System, new DefaultSamlSigninStateSerializer(), NullLogger<SamlSigninStateStore>.Instance);
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    // --- EF-specific tests ---

    [Fact]
    public async Task RetrieveSigninRequestStateAsync_WhenStateExpired_ExpectNull()
    {
        var state = CreateState();
        Guid stateId;

        await using (var context = new PersistedGrantDbContext(_options))
        {
            stateId = await new SamlSigninStateStore(context, TimeProvider.System, new DefaultSamlSigninStateSerializer(), NullLogger<SamlSigninStateStore>.Instance)
                .StoreSigninRequestStateAsync(state, _ct);
        }

        // Manually expire the entity
        await using (var context = new PersistedGrantDbContext(_options))
        {
            var entity = await context.SamlSigninStates.SingleAsync(x => x.StateId == stateId, _ct);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var result = await new SamlSigninStateStore(context, TimeProvider.System, new DefaultSamlSigninStateSerializer(), NullLogger<SamlSigninStateStore>.Instance)
                .RetrieveSigninRequestStateAsync(stateId, _ct);
            result.ShouldBeNull();
        }
    }

    [Fact]
    public async Task StoreSigninRequestStateAsync_WhenCustomExpirySet_ExpectExpiryPersistedFromModel()
    {
        var customExpiry = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var state = CreateState();
        state.ExpiresAtUtc = customExpiry;
        Guid stateId;

        await using (var context = new PersistedGrantDbContext(_options))
        {
            stateId = await new SamlSigninStateStore(context, TimeProvider.System, new DefaultSamlSigninStateSerializer(), NullLogger<SamlSigninStateStore>.Instance)
                .StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var entity = await context.SamlSigninStates.SingleAsync(x => x.StateId == stateId, _ct);
            entity.ExpiresAtUtc.ShouldBe(customExpiry);
        }
    }
}
