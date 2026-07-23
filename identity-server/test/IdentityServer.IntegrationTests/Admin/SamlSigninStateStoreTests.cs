// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Integration tests for the IStore-backed ISamlSigninStateStore implementation.
/// Tests cover all 4 interface methods against a real SQLite database.
/// </summary>
public sealed class SamlSigninStateStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private ISamlSigninStateStore Store => _fixture.SamlSigninStateStore;

    private static SamlAuthenticationState BuildState(DateTime? expiresAt = null) =>
        new()
        {
            ServiceProviderEntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerService = new IndexedEndpoint
            {
                Location = "https://sp.example.com/saml/acs",
                Binding = SamlBinding.HttpPost,
                Index = 0,
                IsDefault = true
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAt ?? DateTime.UtcNow.AddMinutes(5),
            IsIdpInitiated = false,
            RelayState = "https://sp.example.com/dashboard"
        };

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    // ─────────────────────────── Store + Retrieve ───────────────────────────

    [Fact]
    public async Task StoreAndRetrieve_RoundTrips()
    {
        var state = BuildState();

        var stateId = await Store.StoreSigninRequestStateAsync(state, _ct);

        var retrieved = await Store.RetrieveSigninRequestStateAsync(stateId, _ct);

        retrieved.ShouldNotBeNull();
        retrieved.ServiceProviderEntityId.ShouldBe(state.ServiceProviderEntityId);
        retrieved.RelayState.ShouldBe(state.RelayState);
        retrieved.IsIdpInitiated.ShouldBe(false);
        retrieved.AssertionConsumerService.Location.ShouldBe("https://sp.example.com/saml/acs");
    }

    [Fact]
    public async Task RetrieveNonexistent_ReturnsNull()
    {
        var result = await Store.RetrieveSigninRequestStateAsync(Guid.CreateVersion7(), _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RetrieveExpired_ReturnsNull()
    {
        var state = BuildState(expiresAt: DateTime.UtcNow.AddSeconds(-10));

        var stateId = await Store.StoreSigninRequestStateAsync(state, _ct);

        var result = await Store.RetrieveSigninRequestStateAsync(stateId, _ct);

        result.ShouldBeNull();
    }

    // ─────────────────────────── Update ───────────────────────────

    [Fact]
    public async Task Update_ModifiesState()
    {
        var state = BuildState();
        var stateId = await Store.StoreSigninRequestStateAsync(state, _ct);

        // Update with denial error
        state.DenialError = InteractionError.AccessDenied;
        state.DenialErrorDescription = "User cancelled authentication";
        await Store.UpdateSigninRequestStateAsync(stateId, state, _ct);

        var retrieved = await Store.RetrieveSigninRequestStateAsync(stateId, _ct);

        retrieved.ShouldNotBeNull();
        retrieved.DenialError.ShouldBe(InteractionError.AccessDenied);
        retrieved.DenialErrorDescription.ShouldBe("User cancelled authentication");
    }

    [Fact]
    public async Task UpdateExpiredState_DoesNothing()
    {
        var state = BuildState(expiresAt: DateTime.UtcNow.AddSeconds(-10));
        var stateId = await Store.StoreSigninRequestStateAsync(state, _ct);

        state.DenialError = InteractionError.AccessDenied;

        // Update should silently fail (state is expired)
        await Should.NotThrowAsync(() =>
            Store.UpdateSigninRequestStateAsync(stateId, state, _ct));

        // Retrieve confirms state is not available
        var result = await Store.RetrieveSigninRequestStateAsync(stateId, _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateNonexistent_DoesNotThrow() =>
        await Should.NotThrowAsync(() =>
            Store.UpdateSigninRequestStateAsync(Guid.CreateVersion7(), BuildState(), _ct));

    // ─────────────────────────── Remove ───────────────────────────

    [Fact]
    public async Task Remove_ThenRetrieve_ReturnsNull()
    {
        var state = BuildState();
        var stateId = await Store.StoreSigninRequestStateAsync(state, _ct);

        await Store.RemoveSigninRequestStateAsync(stateId, _ct);

        var result = await Store.RetrieveSigninRequestStateAsync(stateId, _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveNonexistent_DoesNotThrow() =>
        await Should.NotThrowAsync(() =>
            Store.RemoveSigninRequestStateAsync(Guid.CreateVersion7(), _ct));

    // ─────────────────────────── Non-V7 GUID handling ───────────────────────────

    [Fact]
    public async Task RetrieveWithNonV7Guid_ReturnsNull()
    {
        var result = await Store.RetrieveSigninRequestStateAsync(Guid.NewGuid(), _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateWithNonV7Guid_DoesNotThrow() =>
        await Should.NotThrowAsync(() =>
            Store.UpdateSigninRequestStateAsync(Guid.NewGuid(), BuildState(), _ct));

    [Fact]
    public async Task RemoveWithNonV7Guid_DoesNotThrow() =>
        await Should.NotThrowAsync(() =>
            Store.RemoveSigninRequestStateAsync(Guid.NewGuid(), _ct));

    // ─────────────────────────── Validation ───────────────────────────

    [Fact]
    public async Task StoreWithoutExpiration_Throws()
    {
        var state = BuildState();
        state.ExpiresAtUtc = default;

        await Should.ThrowAsync<ArgumentException>(() =>
            Store.StoreSigninRequestStateAsync(state, _ct));
    }
}
