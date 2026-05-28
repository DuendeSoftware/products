// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.Storage.Stores;

public class SamlSigninStateStoreTests : IntegrationTest<SamlSigninStateStoreTests, PersistedGrantDbContext, OperationalStoreOptions>
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public SamlSigninStateStoreTests(DatabaseProviderFixture<PersistedGrantDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new PersistedGrantDbContext(options);
            context.Database.EnsureCreated();
        }
    }

    private static SamlAuthenticationState CreateState() =>
        new()
        {
            ServiceProviderEntityId = "https://sp.example.com",
            RelayState = "relay",
            IsIdpInitiated = false,
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            AssertionConsumerService = new IndexedEndpoint
            {
                Binding = SamlBinding.HttpPost,
                Location = "https://sp.example.com/acs",
                Index = 0,
                IsDefault = true,
            },
        };

    private static SamlSigninStateStore CreateStore(
        PersistedGrantDbContext context,
        TimeProvider? timeProvider = null,
        TimeSpan? signinStateLifetime = null) =>
        new(
            context,
            timeProvider ?? TimeProvider.System,
            new DefaultSamlSigninStateSerializer(),
            NullLogger<SamlSigninStateStore>.Instance);

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreSigninRequestStateAsync_WhenSuccessful_ExpectStateRetrievable(DbContextOptions<PersistedGrantDbContext> options)
    {
        var state = CreateState();
        Guid stateId;

        await using (var context = new PersistedGrantDbContext(options))
        {
            stateId = await CreateStore(context).StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var retrieved = await CreateStore(context).RetrieveSigninRequestStateAsync(stateId, _ct);

            retrieved.ShouldNotBeNull();
            retrieved.ServiceProviderEntityId.ShouldBe(state.ServiceProviderEntityId);
            retrieved.RelayState.ShouldBe(state.RelayState);
            retrieved.IsIdpInitiated.ShouldBe(state.IsIdpInitiated);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RetrieveSigninRequestStateAsync_WhenStateDoesNotExist_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using var context = new PersistedGrantDbContext(options);
        var result = await CreateStore(context).RetrieveSigninRequestStateAsync(Guid.NewGuid(), _ct);
        result.ShouldBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RetrieveSigninRequestStateAsync_WhenStateExpired_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        var state = CreateState();
        Guid stateId;

        await using (var context = new PersistedGrantDbContext(options))
        {
            stateId = await CreateStore(context).StoreSigninRequestStateAsync(state, _ct);
        }

        // Manually expire the entity
        await using (var context = new PersistedGrantDbContext(options))
        {
            var entity = await context.SamlSigninStates.SingleAsync(x => x.StateId == stateId, _ct);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).RetrieveSigninRequestStateAsync(stateId, _ct);
            result.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveSigninRequestStateAsync_WhenStateExists_ExpectStateDeleted(DbContextOptions<PersistedGrantDbContext> options)
    {
        var state = CreateState();
        Guid stateId;

        await using (var context = new PersistedGrantDbContext(options))
        {
            stateId = await CreateStore(context).StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            await CreateStore(context).RemoveSigninRequestStateAsync(stateId, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var result = await CreateStore(context).RetrieveSigninRequestStateAsync(stateId, _ct);
            result.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveSigninRequestStateAsync_WhenStateDoesNotExist_ExpectNoException(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using var context = new PersistedGrantDbContext(options);
        var stateId = Guid.NewGuid();

        // Should not throw even if state doesn't exist
        await CreateStore(context).RemoveSigninRequestStateAsync(stateId, _ct);
        await CreateStore(context).RemoveSigninRequestStateAsync(stateId, _ct);
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RetrieveSigninRequestStateAsync_WhenCalledMultipleTimes_ExpectStateNotRemoved(DbContextOptions<PersistedGrantDbContext> options)
    {
        var state = CreateState();
        Guid stateId;

        await using (var context = new PersistedGrantDbContext(options))
        {
            stateId = await CreateStore(context).StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var first = await CreateStore(context).RetrieveSigninRequestStateAsync(stateId, _ct);
            var second = await CreateStore(context).RetrieveSigninRequestStateAsync(stateId, _ct);

            first.ShouldNotBeNull();
            second.ShouldNotBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreSigninRequestStateAsync_WhenCustomExpirySet_ExpectExpiryPersistedFromModel(DbContextOptions<PersistedGrantDbContext> options)
    {
        var customExpiry = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var state = CreateState();
        state.ExpiresAtUtc = customExpiry;
        Guid stateId;

        await using (var context = new PersistedGrantDbContext(options))
        {
            stateId = await CreateStore(context).StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var entity = await context.SamlSigninStates.SingleAsync(x => x.StateId == stateId, _ct);
            entity.ExpiresAtUtc.ShouldBe(customExpiry);
        }
    }
}
