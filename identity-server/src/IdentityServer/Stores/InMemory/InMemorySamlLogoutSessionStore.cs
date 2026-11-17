// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using Duende.IdentityServer.Saml;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory implementation of <see cref="ISamlLogoutSessionStore"/>.
/// </summary>
public sealed class InMemorySamlLogoutSessionStore : ISamlLogoutSessionStore
{
    /// <summary>
    /// Hard cap on stored sessions to prevent unbounded memory growth (DoS protection).
    /// The cap applies per partition, so with Spaces enabled each space can hold up to this many sessions.
    /// </summary>
    private const int MaxEntries = 10_000;

    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemorySamlLogoutSessionStore> _logger;
    private readonly PartitionedState<SamlLogoutSessionState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySamlLogoutSessionStore"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    /// <remarks>
    /// This overload holds a single partition and isn't split by space; to partition by space use
    /// <see cref="InMemorySamlLogoutSessionStore(TimeProvider, ILogger{InMemorySamlLogoutSessionStore}, InMemoryStoreState)"/>.
    /// </remarks>
    public InMemorySamlLogoutSessionStore(TimeProvider timeProvider, ILogger<InMemorySamlLogoutSessionStore> logger)
        : this(timeProvider, logger, new InMemoryStoreState())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySamlLogoutSessionStore"/> class
    /// that partitions its data using the given state source.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="state">The state source, which decides how data is partitioned.</param>
    public InMemorySamlLogoutSessionStore(
        TimeProvider timeProvider,
        ILogger<InMemorySamlLogoutSessionStore> logger,
        InMemoryStoreState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _timeProvider = timeProvider;
        _logger = logger;
        _state = new(state.CurrentSpace);
    }

    /// <inheritdoc/>
    public Task StoreAsync(SamlLogoutSession session, Ct ct)
    {
        if (session.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML logout session.", nameof(session));
        }

        var state = _state.Current;

        CleanupExpired(state);

        if (state.Sessions.Count >= MaxEntries)
        {
            _logger.SAMLLogoutSessionStoreHasReachedTheMaximum(MaxEntries, session.LogoutId);
            return Task.CompletedTask;
        }

        // Defensive copy so callers cannot mutate the stored session.
        var stored = new SamlLogoutSession
        {
            LogoutId = session.LogoutId,
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>(session.ExpectedResponses),
            CreatedUtc = session.CreatedUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            SkippedSpCount = session.SkippedSpCount

        };

        state.Sessions[stored.LogoutId] = stored;

        foreach (var (requestId, _) in stored.ExpectedResponses)
        {
            state.RequestIndex[requestId] = session.LogoutId;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<SamlLogoutSession?> GetByLogoutIdAsync(string logoutId, Ct ct)
    {
        if (!_state.TryGetCurrent(out var state))
        {
            return Task.FromResult<SamlLogoutSession?>(null);
        }

        if (!state.Sessions.TryGetValue(logoutId, out var session))
        {
            return Task.FromResult<SamlLogoutSession?>(null);
        }

        if (IsExpired(session))
        {
            Remove(state, session);
            return Task.FromResult<SamlLogoutSession?>(null);
        }

        // Return a snapshot so callers don't race with TryRecordResponseAsync.
        lock (state.SessionLock)
        {
            var snapshot = new SamlLogoutSession
            {
                LogoutId = session.LogoutId,
                ExpectedResponses = new Dictionary<string, ExpectedSpLogout>(session.ExpectedResponses),
                CreatedUtc = session.CreatedUtc,
                ExpiresAtUtc = session.ExpiresAtUtc,
                SkippedSpCount = session.SkippedSpCount
            };
            return Task.FromResult<SamlLogoutSession?>(snapshot);
        }
    }

    /// <inheritdoc/>
    public Task<bool> TryRecordResponseAsync(string requestId, string issuer, bool success, Ct ct)
    {
        if (!_state.TryGetCurrent(out var state))
        {
            return Task.FromResult(false);
        }

        if (!state.RequestIndex.TryGetValue(requestId, out var logoutId))
        {
            return Task.FromResult(false);
        }

        if (!state.Sessions.TryGetValue(logoutId, out var session))
        {
            return Task.FromResult(false);
        }

        if (IsExpired(session))
        {
            Remove(state, session);
            return Task.FromResult(false);
        }

        lock (state.SessionLock)
        {
            if (!session.ExpectedResponses.TryGetValue(requestId, out var expected))
            {
                return Task.FromResult(false);
            }

            if (!string.Equals(expected.SpEntityId, issuer, StringComparison.Ordinal))
            {
                _logger.SAMLLogoutResponseIssuerMismatchForRequestIdRequestId(requestId, expected.SpEntityId, issuer);
                return Task.FromResult(false);
            }

            var response = new SamlSpLogoutResponse(success, _timeProvider.GetUtcNow());
            session.ExpectedResponses[requestId] = expected with { Response = response };
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string logoutId, Ct ct)
    {
        if (_state.TryGetCurrent(out var state) && state.Sessions.TryRemove(logoutId, out var session))
        {
            RemoveSecondaryIndex(state, session);
        }

        return Task.CompletedTask;
    }

    private bool IsExpired(SamlLogoutSession session) =>
        _timeProvider.GetUtcNow().UtcDateTime > session.ExpiresAtUtc;

    private static void Remove(SamlLogoutSessionState state, SamlLogoutSession session)
    {
        state.Sessions.TryRemove(session.LogoutId, out _);
        RemoveSecondaryIndex(state, session);
    }

    private static void RemoveSecondaryIndex(SamlLogoutSessionState state, SamlLogoutSession session)
    {
        lock (state.SessionLock)
        {
            foreach (var (requestId, _) in session.ExpectedResponses)
            {
                state.RequestIndex.TryRemove(requestId, out _);
            }
        }
    }

    /// <summary>
    /// Removes all expired sessions. Called on every <see cref="StoreAsync"/> as a
    /// simple eviction strategy. Acceptable for an in-memory store with a 10K cap;
    /// production implementations (EF, distributed cache) would use TTL or background jobs.
    /// </summary>
    private void CleanupExpired(SamlLogoutSessionState state)
    {
        foreach (var (_, session) in state.Sessions)
        {
            if (IsExpired(session))
            {
                Remove(state, session);
            }
        }
    }
}

internal sealed class SamlLogoutSessionState
{
    // Primary index: logoutId → session (thread-safe for concurrent add/remove/lookup)
    public ConcurrentDictionary<string, SamlLogoutSession> Sessions { get; } = new();

    // Secondary index: requestId → logoutId (thread-safe for concurrent add/remove/lookup)
    public ConcurrentDictionary<string, string> RequestIndex { get; } = new();

    // Protects reads and mutations of individual session ExpectedResponses dictionaries,
    // which are plain Dictionary<TKey,TValue> (not thread-safe).
    public Lock SessionLock { get; } = new();
}
