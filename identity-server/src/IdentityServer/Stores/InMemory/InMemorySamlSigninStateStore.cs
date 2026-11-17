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
public sealed class InMemorySamlSigninStateStore : ISamlSigninStateStore
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemorySamlSigninStateStore> _logger;
    private readonly PartitionedState<SamlSigninState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySamlSigninStateStore"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    /// <remarks>
    /// This overload holds a single partition and isn't split by space; to partition by space use
    /// <see cref="InMemorySamlSigninStateStore(TimeProvider, ILogger{InMemorySamlSigninStateStore}, InMemoryStoreState)"/>.
    /// </remarks>
    public InMemorySamlSigninStateStore(TimeProvider timeProvider, ILogger<InMemorySamlSigninStateStore> logger)
        : this(timeProvider, logger, new InMemoryStoreState())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySamlSigninStateStore"/> class
    /// that partitions its data using the given state source.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="state">The state source, which decides how data is partitioned.</param>
    public InMemorySamlSigninStateStore(
        TimeProvider timeProvider,
        ILogger<InMemorySamlSigninStateStore> logger,
        InMemoryStoreState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _timeProvider = timeProvider;
        _logger = logger;
        _state = new(state.CurrentSpace);
    }

    /// <inheritdoc/>
    public Task<Guid> StoreSigninRequestStateAsync(SamlAuthenticationState state, Ct ct)
    {
        if (state.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML signin state.", nameof(state));
        }

        var id = Guid.NewGuid();
        _state.Current.Items[id] = state;
        return Task.FromResult(id);
    }

    /// <inheritdoc/>
    public Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        if (!_state.TryGetCurrent(out var current))
        {
            return Task.FromResult<SamlAuthenticationState?>(null);
        }

        var items = current.Items;

        if (!items.TryGetValue(stateId, out var state))
        {
            return Task.FromResult<SamlAuthenticationState?>(null);
        }

        // Enforce TTL — return null for expired state (same as not found)
        if (_timeProvider.GetUtcNow().UtcDateTime > state.ExpiresAtUtc)
        {
            items.TryRemove(stateId, out _);
            return Task.FromResult<SamlAuthenticationState?>(null);
        }

        return Task.FromResult<SamlAuthenticationState?>(state);
    }

    /// <inheritdoc/>
    public Task RemoveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        if (_state.TryGetCurrent(out var current))
        {
            current.Items.TryRemove(stateId, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateSigninRequestStateAsync(Guid stateId, SamlAuthenticationState state, Ct ct)
    {
        if (!_state.TryGetCurrent(out var current))
        {
            _logger.SAMLSigninStateStateIdNotFoundForUpdate(stateId);
            return Task.CompletedTask;
        }

        var items = current.Items;

        if (!items.TryGetValue(stateId, out var existing))
        {
            _logger.SAMLSigninStateStateIdNotFoundForUpdate(stateId);
            return Task.CompletedTask;
        }

        // Enforce TTL — do not update expired state
        if (_timeProvider.GetUtcNow().UtcDateTime > existing.ExpiresAtUtc)
        {
            items.TryRemove(stateId, out _);
            _logger.SAMLSigninStateStateIdExpiredCannotUpdate(stateId);
            return Task.CompletedTask;
        }

        items[stateId] = state;
        return Task.CompletedTask;
    }
}

internal sealed class SamlSigninState
{
    public ConcurrentDictionary<Guid, SamlAuthenticationState> Items { get; } = new();
}
