// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Saml;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.SamlLogoutSession;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SamlLogoutSessionStore(
    SamlLogoutSessionRepository repository,
    TimeProvider timeProvider,
    ILogger<SamlLogoutSessionStore> logger) : ISamlLogoutSessionStore
{
    /// <inheritdoc/>
    public async Task StoreAsync(Saml.SamlLogoutSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.Store");

        if (session.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML logout session.", nameof(session));
        }

        var id = UuidV7.New();
        var serializedSession = JsonSamlLogoutSessionSerializer.Serialize(session);
        var dso = new SamlLogoutSessionDso.V1
        {
            LogoutId = session.LogoutId,
            RequestIds = session.ExpectedResponses.Keys.ToList(),
            SerializedSession = serializedSession,
            ExpiresAtUtcTicks = session.ExpiresAtUtc.ToUniversalTime().Ticks
        };
        var expiration = Expiration.AtAbsolute(new DateTimeOffset(session.ExpiresAtUtc.ToUniversalTime(), TimeSpan.Zero));

        var result = await repository.CreateAsync(id, dso, expiration, ct);

        if (result != CreateResult.Success)
        {
            logger.SamlLogoutSessionStoreFailed(LogLevel.Error, session.LogoutId, result.ToString());
            throw new InvalidOperationException($"Could not store SAML logout session {session.LogoutId}: {result}");
        }

        logger.SamlLogoutSessionStored(LogLevel.Debug, session.LogoutId);
    }

    /// <inheritdoc/>
    public async Task<Saml.SamlLogoutSession?> GetByLogoutIdAsync(string logoutId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.GetByLogoutId");

        var entry = await repository.TryReadByLogoutIdAsync(logoutId, ct);

        if (entry is null)
        {
            logger.SamlLogoutSessionNotFound(LogLevel.Debug, logoutId);
            return null;
        }

        var expiresAtUtc = new DateTime(entry.Value.Dso.ExpiresAtUtcTicks, DateTimeKind.Utc);

        if (expiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            logger.SamlLogoutSessionExpired(LogLevel.Debug, logoutId);
            return null;
        }

        var session = JsonSamlLogoutSessionSerializer.Deserialize(entry.Value.Dso.SerializedSession);

        if (session is null)
        {
            logger.SamlLogoutSessionDeserializationFailed(LogLevel.Warning, logoutId);
            return null;
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<bool> TryRecordResponseAsync(string requestId, string issuer, bool success, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.TryRecordResponse");

        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var result = await TryRecordResponseCoreAsync(requestId, issuer, success, ct);
            if (result != RecordResponseResult.ConcurrencyConflict)
            {
                return result == RecordResponseResult.Success;
            }

            if (attempt < maxRetries - 1)
            {
                logger.SamlLogoutResponseConcurrencyRetry(LogLevel.Debug, requestId, attempt + 1);
            }
        }

        logger.SamlLogoutResponseConcurrencyExhausted(LogLevel.Warning, requestId, maxRetries);
        return false;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string logoutId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlLogoutSessionStore.Remove");

        await repository.DeleteByLogoutIdAsync(logoutId, ct);

        logger.SamlLogoutSessionRemoved(LogLevel.Debug, logoutId);
    }

    private async Task<RecordResponseResult> TryRecordResponseCoreAsync(
        string requestId, string issuer, bool success, Ct ct)
    {
        var entry = await repository.TryReadByRequestIdAsync(requestId, ct);

        if (entry is null)
        {
            return RecordResponseResult.NotFound;
        }

        var expiresAtUtc = new DateTime(entry.Value.Dso.ExpiresAtUtcTicks, DateTimeKind.Utc);

        if (expiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return RecordResponseResult.NotFound;
        }

        var session = JsonSamlLogoutSessionSerializer.Deserialize(entry.Value.Dso.SerializedSession);

        if (session is null)
        {
            logger.SamlLogoutSessionDeserializationFailedForRequestId(LogLevel.Warning, requestId);
            return RecordResponseResult.NotFound;
        }

        if (!session.ExpectedResponses.TryGetValue(requestId, out var expected))
        {
            return RecordResponseResult.NotFound;
        }

        if (!string.Equals(expected.SpEntityId, issuer, StringComparison.Ordinal))
        {
            logger.SamlLogoutResponseIssuerMismatch(LogLevel.Warning, requestId, expected.SpEntityId, issuer);
            return RecordResponseResult.NotFound;
        }

        var response = new SamlSpLogoutResponse(success, timeProvider.GetUtcNow());
        session.ExpectedResponses[requestId] = expected with { Response = response };

        var newJson = JsonSamlLogoutSessionSerializer.Serialize(session);
        var updatedDso = entry.Value.Dso with { SerializedSession = newJson };
        var expiration = Expiration.AtAbsolute(new DateTimeOffset(expiresAtUtc, TimeSpan.Zero));

        var updateResult = await repository.UpdateAsync(
            UuidV7.From(entry.Value.Id), updatedDso, entry.Value.Version, expiration, ct);

        switch (updateResult)
        {
            case UpdateResult.Success:
                return RecordResponseResult.Success;
            case UpdateResult.UnexpectedVersion:
                return RecordResponseResult.ConcurrencyConflict;
            case UpdateResult.KeyConflict:
                logger.SamlLogoutSessionKeyConflict(LogLevel.Warning, requestId);
                return RecordResponseResult.NotFound;
            case UpdateResult.DoesNotExist:
            default:
                return RecordResponseResult.NotFound;
        }
    }

    private enum RecordResponseResult
    {
        Success,
        NotFound,
        ConcurrencyConflict
    }
}
