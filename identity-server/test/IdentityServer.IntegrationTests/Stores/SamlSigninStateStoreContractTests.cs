// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for ISamlSigninStateStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class SamlSigninStateStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<ISamlSigninStateStore> CreateStore();

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static SamlAuthenticationState CreateState() =>
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

    [Fact]
    public async Task StoreSigninRequestStateAsync_WhenSuccessful_ExpectStateRetrievable()
    {
        var state = CreateState();
        Guid stateId;

        await using (var handle = CreateStore())
        {
            stateId = await handle.Store.StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var handle = CreateStore())
        {
            var retrieved = await handle.Store.RetrieveSigninRequestStateAsync(stateId, _ct);

            retrieved.ShouldNotBeNull();
            retrieved.ServiceProviderEntityId.ShouldBe(state.ServiceProviderEntityId);
            retrieved.RelayState.ShouldBe(state.RelayState);
            retrieved.IsIdpInitiated.ShouldBe(state.IsIdpInitiated);
        }
    }

    [Fact]
    public async Task RetrieveSigninRequestStateAsync_WhenStateDoesNotExist_ExpectNull()
    {
        await using var handle = CreateStore();
        var result = await handle.Store.RetrieveSigninRequestStateAsync(Guid.NewGuid(), _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveSigninRequestStateAsync_WhenStateExists_ExpectStateDeleted()
    {
        var state = CreateState();
        Guid stateId;

        await using (var handle = CreateStore())
        {
            stateId = await handle.Store.StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var handle = CreateStore())
        {
            await handle.Store.RemoveSigninRequestStateAsync(stateId, _ct);
        }

        await using (var handle = CreateStore())
        {
            var result = await handle.Store.RetrieveSigninRequestStateAsync(stateId, _ct);
            result.ShouldBeNull();
        }
    }

    [Fact]
    public async Task RemoveSigninRequestStateAsync_WhenStateDoesNotExist_ExpectNoException()
    {
        await using var handle = CreateStore();
        var stateId = Guid.NewGuid();

        // Should not throw even if state doesn't exist
        await handle.Store.RemoveSigninRequestStateAsync(stateId, _ct);
        await handle.Store.RemoveSigninRequestStateAsync(stateId, _ct);
    }

    [Fact]
    public async Task RetrieveSigninRequestStateAsync_WhenCalledMultipleTimes_ExpectStateNotRemoved()
    {
        var state = CreateState();
        Guid stateId;

        await using (var handle = CreateStore())
        {
            stateId = await handle.Store.StoreSigninRequestStateAsync(state, _ct);
        }

        await using (var handle = CreateStore())
        {
            var first = await handle.Store.RetrieveSigninRequestStateAsync(stateId, _ct);
            var second = await handle.Store.RetrieveSigninRequestStateAsync(stateId, _ct);

            first.ShouldNotBeNull();
            second.ShouldNotBeNull();
        }
    }
}
