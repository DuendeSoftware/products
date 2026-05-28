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
public sealed class InMemorySamlLogoutSessionStore(
    TimeProvider timeProvider,
    ILogger<InMemorySamlLogoutSessionStore> logger) : ISamlLogoutSessionStore
{
    /// <summary>
    /// Hard cap on stored sessions to prevent unbounded memory growth (DoS protection).
    /// </summary>
    private const int MaxEntries = 10_000;

    // Primary index: logoutId → session (thread-safe for concurrent add/remove/lookup)
    private readonly ConcurrentDictionary<string, SamlLogoutSession> _sessions = new();

    // Secondary index: requestId → logoutId (thread-safe for concurrent add/remove/lookup)
    private readonly ConcurrentDictionary<string, string> _requestIndex = new();

    // Protects reads and mutations of individual session ExpectedResponses dictionaries,
    // which are plain Dictionary<TKey,TValue> (not thread-safe).
    private readonly Lock _sessionLock = new();

    /// <inheritdoc/>
    public Task StoreAsync(SamlLogoutSession session, Ct ct)
    {
        if (session.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML logout session.", nameof(session));
        }

        CleanupExpired();

        if (_sessions.Count >= MaxEntries)
        {
            logger.LogWarning(
                "SAML logout session store has reached the maximum capacity of {MaxEntries}. " +
                "Rejecting new session for logoutId {LogoutId}",
                MaxEntries, session.LogoutId);
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

        _sessions[stored.LogoutId] = stored;

        foreach (var (requestId, _) in stored.ExpectedResponses)
        {
            _requestIndex[requestId] = session.LogoutId;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<SamlLogoutSession?> GetByLogoutIdAsync(string logoutId, Ct ct)
    {
        if (!_sessions.TryGetValue(logoutId, out var session))
        {
            return Task.FromResult<SamlLogoutSession?>(null);
        }

        if (IsExpired(session))
        {
            Remove(session);
            return Task.FromResult<SamlLogoutSession?>(null);
        }

        // Return a snapshot so callers don't race with TryRecordResponseAsync.
        lock (_sessionLock)
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
        if (!_requestIndex.TryGetValue(requestId, out var logoutId))
        {
            return Task.FromResult(false);
        }

        if (!_sessions.TryGetValue(logoutId, out var session))
        {
            return Task.FromResult(false);
        }

        if (IsExpired(session))
        {
            Remove(session);
            return Task.FromResult(false);
        }

        lock (_sessionLock)
        {
            if (!session.ExpectedResponses.TryGetValue(requestId, out var expected))
            {
                return Task.FromResult(false);
            }

            if (!string.Equals(expected.SpEntityId, issuer, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "SAML logout response issuer mismatch for requestId {RequestId}. " +
                    "Expected {ExpectedIssuer}, received {ActualIssuer}",
                    requestId, expected.SpEntityId, issuer);
                return Task.FromResult(false);
            }

            var response = new SamlSpLogoutResponse(success, timeProvider.GetUtcNow());
            session.ExpectedResponses[requestId] = expected with { Response = response };
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string logoutId, Ct ct)
    {
        if (_sessions.TryRemove(logoutId, out var session))
        {
            RemoveSecondaryIndex(session);
        }

        return Task.CompletedTask;
    }

    private bool IsExpired(SamlLogoutSession session) =>
        timeProvider.GetUtcNow().UtcDateTime > session.ExpiresAtUtc;

    private void Remove(SamlLogoutSession session)
    {
        _sessions.TryRemove(session.LogoutId, out _);
        RemoveSecondaryIndex(session);
    }

    private void RemoveSecondaryIndex(SamlLogoutSession session)
    {
        lock (_sessionLock)
        {
            foreach (var (requestId, _) in session.ExpectedResponses)
            {
                _requestIndex.TryRemove(requestId, out _);
            }
        }
    }

    /// <summary>
    /// Removes all expired sessions. Called on every <see cref="StoreAsync"/> as a
    /// simple eviction strategy. Acceptable for an in-memory store with a 10K cap;
    /// production implementations (EF, distributed cache) would use TTL or background jobs.
    /// </summary>
    private void CleanupExpired()
    {
        foreach (var (_, session) in _sessions)
        {
            if (IsExpired(session))
            {
                Remove(session);
            }
        }
    }
}
