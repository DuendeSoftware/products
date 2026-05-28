// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using Duende.IdentityServer.Saml;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory implementation of <see cref="ISamlSigninStateStore"/>.
/// </summary>
public sealed class InMemorySamlSigninStateStore(
    TimeProvider timeProvider,
    ILogger<InMemorySamlSigninStateStore> logger) : ISamlSigninStateStore
{
    private readonly ConcurrentDictionary<Guid, SamlAuthenticationState> _store = new();

    /// <inheritdoc/>
    public Task<Guid> StoreSigninRequestStateAsync(SamlAuthenticationState state, Ct ct)
    {
        if (state.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML signin state.", nameof(state));
        }

        var id = Guid.NewGuid();
        _store[id] = state;
        return Task.FromResult(id);
    }

    /// <inheritdoc/>
    public Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        if (!_store.TryGetValue(stateId, out var state))
        {
            return Task.FromResult<SamlAuthenticationState?>(null);
        }

        // Enforce TTL — return null for expired state (same as not found)
        if (timeProvider.GetUtcNow().UtcDateTime > state.ExpiresAtUtc)
        {
            _store.TryRemove(stateId, out _);
            return Task.FromResult<SamlAuthenticationState?>(null);
        }

        return Task.FromResult<SamlAuthenticationState?>(state);
    }

    /// <inheritdoc/>
    public Task RemoveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        _store.TryRemove(stateId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateSigninRequestStateAsync(Guid stateId, SamlAuthenticationState state, Ct ct)
    {
        if (!_store.TryGetValue(stateId, out var existing))
        {
            logger.LogWarning("SAML signin state {StateId} not found for update", stateId);
            return Task.CompletedTask;
        }

        // Enforce TTL — do not update expired state
        if (timeProvider.GetUtcNow().UtcDateTime > existing.ExpiresAtUtc)
        {
            _store.TryRemove(stateId, out _);
            logger.LogWarning("SAML signin state {StateId} expired, cannot update", stateId);
            return Task.CompletedTask;
        }

        _store[stateId] = state;
        return Task.CompletedTask;
    }
}
