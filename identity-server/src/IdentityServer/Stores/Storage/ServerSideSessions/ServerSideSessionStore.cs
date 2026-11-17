// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.ServerSideSessions;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ServerSideSessionStore(
    ServerSideSessionRepository repository,
    ILogger<ServerSideSessionStore> logger) : IServerSideSessionStore
{
    /// <inheritdoc/>
    public async Task<ServerSideSession?> GetSessionAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetSession");

        var result = await repository.TryReadByKeyAsync(key, ct);
        return result is null ? null : MapToModel(result.Value.Dso, result.Value.Version);
    }

    /// <inheritdoc/>
    public async Task CreateSessionAsync(ServerSideSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.CreateSession");

        var result = await repository.CreateAsync(session, ct);

        if (result != CreateResult.Success)
        {
            logger.FailedToCreateSessionWithKeyKeyResult(session.Key, result);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateSessionAsync(ServerSideSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.UpdateSession");

        var current = await repository.TryReadByKeyAsync(session.Key, ct);
        if (current is null)
        {
            logger.NoServerSideSessionKeyFoundUpdateSkipped(session.Key);
            return false;
        }

        var (id, version, _) = current.Value;
        var expectedVersion = session.Version == 0 ? version : session.Version;
        var result = await repository.UpdateAsync(id, expectedVersion, session, ct);

        if (result is UpdateResult.DoesNotExist or UpdateResult.UnexpectedVersion or UpdateResult.KeyConflict)
        {
            logger.SessionWithKeyKeyCouldNotBeUpdated(session.Key, result);
        }

        return result == UpdateResult.Success;
    }

    /// <inheritdoc/>
    public async Task DeleteSessionAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.DeleteSession");

        await repository.DeleteByKeyAsync(key, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetSessions");

        filter.Validate();

        var results = await repository.QueryByFilterAsync(filter, ct);
        return results.Select(r => MapToModel(r.Dso, r.Version)).ToArray();
    }

    /// <inheritdoc/>
    public async Task DeleteSessionsAsync(SessionFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.DeleteSessions");

        filter.Validate();

        // QueryByFilterAsync pages internally and returns all matching results.
        var results = await repository.QueryByFilterAsync(filter, ct);
        if (results.Count == 0)
        {
            return;
        }

        var ids = results.Select(r => r.Id).ToArray();
        await repository.DeleteByIdsAsync(ids, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetAndRemoveExpiredSessions");

        var expired = await repository.QueryExpiredAsync(count, ct);
        if (expired.Count == 0)
        {
            return Array.Empty<ServerSideSession>();
        }

        var ids = expired.Select(e => e.Id).ToArray();
        await repository.DeleteByIdsAsync(ids, ct);

        return expired.Select(e => ServerSideSessionRepository.DsoToModel(e.Dso)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<QueryResult<ServerSideSession>> QuerySessionsAsync(Ct ct, SessionQuery? filter = null)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.QuerySessions");

        var storageResult = await repository.QueryPagedAsync(filter, ct);

        if (storageResult.Items.Count == 0)
        {
            return new QueryResult<ServerSideSession>
            {
                ResultsToken = null!,
                HasPrevResults = false,
                HasNextResults = false,
                TotalCount = 0,
                TotalPages = 0,
                CurrentPage = 0,
                Results = Array.Empty<ServerSideSession>()
            };
        }

        // Encode both forward and backward tokens so the caller can navigate either direction.
        // Format: "nextToken|previousToken" (either part may be empty). The repository always
        // queries in ascending order, so NextToken/PreviousToken (and HasMoreData/HasPreviousData)
        // keep their natural forward/backward meaning regardless of navigation direction.
        var nextVal = storageResult.NextToken?.Value ?? string.Empty;
        var prevVal = storageResult.PreviousToken?.Value ?? string.Empty;
        var resultsToken = $"{nextVal}|{prevVal}";

        return new QueryResult<ServerSideSession>
        {
            ResultsToken = resultsToken,
            HasPrevResults = storageResult.HasPreviousData,
            HasNextResults = storageResult.HasMoreData,
            TotalCount = storageResult.TotalCount,
            TotalPages = storageResult.TotalPages,
            CurrentPage = null,
            Results = storageResult.Items.Select(ServerSideSessionRepository.DsoToModel).ToArray()
        };
    }

    private static ServerSideSession MapToModel(ServerSideSessionDso.V1 dso, int version)
    {
        var session = ServerSideSessionRepository.DsoToModel(dso);
        session.Version = version;
        return session;
    }
}
