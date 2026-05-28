// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Entity Framework-backed implementation of <see cref="ISamlLogoutSessionStore"/>.
/// </summary>
public sealed class SamlLogoutSessionStore : ISamlLogoutSessionStore
{
    private readonly IPersistedGrantDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamlLogoutSessionStore"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public SamlLogoutSessionStore(
        IPersistedGrantDbContext context,
        TimeProvider timeProvider,
        ILogger<SamlLogoutSessionStore> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StoreAsync(SamlLogoutSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.Store");

        if (session.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML logout session.", nameof(session));
        }

        var entity = session.ToEntity(session.ExpiresAtUtc);

        // Add request-id index rows so TryRecordResponseAsync can look up by requestId
        // without scanning all sessions.
        entity.RequestIndices = session.ExpectedResponses.Keys
            .Select(requestId => new EntityFramework.Entities.SamlLogoutSessionRequestIndex { RequestId = requestId })
            .ToList();

        _context.SamlLogoutSessions.Add(entity);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning("exception storing SAML logout session {LogoutId} in database: {Error}", session.LogoutId, ex.Message);
            throw;
        }

        _logger.LogDebug("stored SAML logout session {LogoutId} in database", session.LogoutId);
    }

    /// <inheritdoc />
    public async Task<SamlLogoutSession?> GetByLogoutIdAsync(string logoutId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.GetByLogoutId");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entity = await _context.SamlLogoutSessions
            .AsNoTracking()
            .Where(x => x.LogoutId == logoutId && x.ExpiresAtUtc > now)
            .SingleOrDefaultAsync(ct);

        var model = entity.ToModel();
        _logger.LogDebug("SAML logout session {LogoutId} found in database: {Found}", logoutId, model != null);
        return model;
    }

    /// <inheritdoc />
    public async Task<bool> TryRecordResponseAsync(string requestId, string issuer, bool success, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.TryRecordResponse");

        // Retry on concurrency conflicts — a concurrent update to the same session
        // (e.g., two SP responses arriving simultaneously) will trigger a conflict
        // due to the Version concurrency token. Retry re-loads and re-applies.
        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var result = await TryRecordResponseCoreAsync(requestId, issuer, success, ct);
            if (result != RecordResponseResult.ConcurrencyConflict)
            {
                return result == RecordResponseResult.Success;
            }

            _logger.LogDebug("Concurrency conflict recording SAML logout response for requestId {RequestId}, retrying (attempt {Attempt})", requestId, attempt + 1);
        }

        _logger.LogWarning("Failed to record SAML logout response for requestId {RequestId} after {MaxRetries} attempts due to concurrency conflicts", requestId, maxRetries);
        return false;
    }

    private async Task<RecordResponseResult> TryRecordResponseCoreAsync(string requestId, string issuer, bool success, Ct ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Use the request-id index to find and load the parent session in a single query.
        var entity = await _context.SamlLogoutSessionRequestIndices
            .Where(x => x.RequestId == requestId)
            .Select(x => x.SamlLogoutSession)
            .Where(x => x.ExpiresAtUtc > now)
            .SingleOrDefaultAsync(ct);

        if (entity is null)
        {
            return RecordResponseResult.NotFound;
        }

        var session = entity.ToModel();
        if (session is null)
        {
            _logger.LogWarning("Failed to deserialize SAML logout session {LogoutId} — skipping", entity.LogoutId);
            return RecordResponseResult.NotFound;
        }

        if (!session.ExpectedResponses.TryGetValue(requestId, out var expected))
        {
            return RecordResponseResult.NotFound;
        }

        if (!string.Equals(expected.SpEntityId, issuer, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "SAML logout response issuer mismatch for requestId {RequestId}. Expected {ExpectedIssuer}, received {ActualIssuer}",
                requestId, expected.SpEntityId, issuer);
            return RecordResponseResult.NotFound;
        }

        // Record the response with current time (not the query time captured above)
        var response = new SamlSpLogoutResponse(success, _timeProvider.GetUtcNow());
        session.ExpectedResponses[requestId] = expected with { Response = response };

        // Re-serialize, increment concurrency token, and save
        entity.SerializedSession = JsonSerializer.Serialize(session, SamlLogoutSessionMappers.JsonOptions);
        entity.Version++;
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Detach the stale entity so the next retry re-reads from the database.
            ((DbContext)_context).Entry(entity).State = EntityState.Detached;
            return RecordResponseResult.ConcurrencyConflict;
        }

        _logger.LogDebug("recorded SAML logout response for requestId {RequestId} (success={Success})", requestId, success);
        return RecordResponseResult.Success;
    }

    private enum RecordResponseResult
    {
        Success,
        NotFound,
        ConcurrencyConflict,
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string logoutId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.Remove");

        // Cascade delete on the FK removes associated request index rows automatically.
        await _context.SamlLogoutSessions
            .Where(x => x.LogoutId == logoutId)
            .ExecuteDeleteAsync(ct);

        _logger.LogDebug("removed SAML logout session {LogoutId} from database", logoutId);
    }
}
