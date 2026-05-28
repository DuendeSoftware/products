// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Stores;

public sealed class InMemorySamlSigninStateStoreTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static SamlAuthenticationState CreateState(DateTimeOffset? createdUtc = null, DateTime? expiresAtUtc = null) =>
        new()
        {
            ServiceProviderEntityId = "https://sp.example.com",
            AssertionConsumerService = new IndexedEndpoint { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost },
            RelayState = "some-relay-state",
            IsIdpInitiated = false,
            CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(15),
        };

    private static InMemorySamlSigninStateStore CreateStore(TimeProvider timeProvider, TimeSpan? signinStateLifetime = null) =>
        new(timeProvider, NullLogger<InMemorySamlSigninStateStore>.Instance);

    [Fact]
    public async Task StoreShouldReturnUniqueStateIds()
    {
        var store = CreateStore(TimeProvider.System);

        var id1 = await store.StoreSigninRequestStateAsync(CreateState(), _ct);
        var id2 = await store.StoreSigninRequestStateAsync(CreateState(), _ct);

        id1.ShouldNotBe(id2);
    }

    [Fact]
    public async Task StoreShouldReturnNonEmptyStateId()
    {
        var store = CreateStore(TimeProvider.System);

        var id = await store.StoreSigninRequestStateAsync(CreateState(), _ct);

        id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task StoreAndRetrieveReturnsState()
    {
        var store = CreateStore(TimeProvider.System);
        var state = CreateState();

        var id = await store.StoreSigninRequestStateAsync(state, _ct);
        var retrieved = await store.RetrieveSigninRequestStateAsync(id, _ct);

        retrieved.ShouldNotBeNull();
        retrieved.ServiceProviderEntityId.ShouldBe(state.ServiceProviderEntityId);
        retrieved.AssertionConsumerService.ShouldBe(state.AssertionConsumerService);
    }

    [Fact]
    public async Task RetrieveDoesNotRemoveState()
    {
        var store = CreateStore(TimeProvider.System);
        var id = await store.StoreSigninRequestStateAsync(CreateState(), _ct);

        var first = await store.RetrieveSigninRequestStateAsync(id, _ct);
        var second = await store.RetrieveSigninRequestStateAsync(id, _ct);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
    }

    [Fact]
    public async Task RemoveDeletesState()
    {
        var store = CreateStore(TimeProvider.System);
        var id = await store.StoreSigninRequestStateAsync(CreateState(), _ct);

        await store.RemoveSigninRequestStateAsync(id, _ct);
        var retrieved = await store.RetrieveSigninRequestStateAsync(id, _ct);

        retrieved.ShouldBeNull();
    }

    [Fact]
    public async Task RetrieveNonexistentStateReturnsNull()
    {
        var store = CreateStore(TimeProvider.System);
        var randomId = Guid.NewGuid();

        var result = await store.RetrieveSigninRequestStateAsync(randomId, _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveNonexistentStateDoesNotThrow()
    {
        var store = CreateStore(TimeProvider.System);
        var randomId = Guid.NewGuid();

        await Should.NotThrowAsync(() => store.RemoveSigninRequestStateAsync(randomId, _ct));
    }

    [Fact]
    public async Task RetrieveReturnsNullWhenStateIsExpired()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = CreateStore(fakeTime);
        var state = CreateState(createdUtc: fakeTime.GetUtcNow(), expiresAtUtc: fakeTime.GetUtcNow().AddMinutes(15).UtcDateTime);

        var id = await store.StoreSigninRequestStateAsync(state, _ct);

        // Advance past the 15-minute TTL
        fakeTime.Advance(TimeSpan.FromMinutes(16));

        var retrieved = await store.RetrieveSigninRequestStateAsync(id, _ct);
        retrieved.ShouldBeNull();
    }

    [Fact]
    public async Task RetrieveReturnsStateWhenNotYetExpired()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = CreateStore(fakeTime);
        var state = CreateState(createdUtc: fakeTime.GetUtcNow(), expiresAtUtc: fakeTime.GetUtcNow().AddMinutes(15).UtcDateTime);

        var id = await store.StoreSigninRequestStateAsync(state, _ct);

        // Advance just under the 15-minute TTL
        fakeTime.Advance(TimeSpan.FromMinutes(14));

        var retrieved = await store.RetrieveSigninRequestStateAsync(id, _ct);
        retrieved.ShouldNotBeNull();
    }

    [Fact]
    public async Task RetrieveReturnsNullWhenStateExceedsCustomTtl()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = CreateStore(fakeTime, signinStateLifetime: TimeSpan.FromMinutes(5));
        var state = CreateState(createdUtc: fakeTime.GetUtcNow(), expiresAtUtc: fakeTime.GetUtcNow().AddMinutes(5).UtcDateTime);

        var id = await store.StoreSigninRequestStateAsync(state, _ct);

        // Advance past the custom 5-minute TTL
        fakeTime.Advance(TimeSpan.FromMinutes(6));

        var retrieved = await store.RetrieveSigninRequestStateAsync(id, _ct);
        retrieved.ShouldBeNull();
    }

    [Fact]
    public async Task RetrieveReturnsStateWhenWithinCustomTtl()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = CreateStore(fakeTime, signinStateLifetime: TimeSpan.FromMinutes(5));
        var state = CreateState(createdUtc: fakeTime.GetUtcNow(), expiresAtUtc: fakeTime.GetUtcNow().AddMinutes(5).UtcDateTime);

        var id = await store.StoreSigninRequestStateAsync(state, _ct);

        // Advance just under the custom 5-minute TTL
        fakeTime.Advance(TimeSpan.FromMinutes(4));

        var retrieved = await store.RetrieveSigninRequestStateAsync(id, _ct);
        retrieved.ShouldNotBeNull();
    }
}
