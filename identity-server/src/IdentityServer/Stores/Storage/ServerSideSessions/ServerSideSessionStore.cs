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
        return result is null ? null : ServerSideSessionRepository.DsoToModel(result.Value.Dso);
    }

    /// <inheritdoc/>
    public async Task CreateSessionAsync(ServerSideSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.CreateSession");

        var result = await repository.CreateAsync(session, ct);

        if (result != CreateResult.Success)
        {
            logger.LogWarning(
                "Failed to create session with key '{Key}'. Result: {Result}.",
                session.Key,
                result);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateSessionAsync(ServerSideSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.UpdateSession");

        var current = await repository.TryReadByKeyAsync(session.Key, ct);
        if (current is null)
        {
            logger.LogDebug(
                "No server-side session '{Key}' found. Update skipped.",
                session.Key);
            return;
        }

        var (id, version, _) = current.Value;
        var result = await repository.UpdateAsync(id, version, session, ct);

        if (result is UpdateResult.DoesNotExist or UpdateResult.UnexpectedVersion or UpdateResult.KeyConflict)
        {
            logger.LogWarning(
                "Session with key '{Key}' could not be updated. Result: {Result}. " +
                "The session may have been concurrently modified or deleted.",
                session.Key,
                result);
        }
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
        return results.Select(r => ServerSideSessionRepository.DsoToModel(r.Dso)).ToArray();
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
        // Format: "nextToken|previousToken" (either part may be empty).
        // The repository normalizes token semantics so NextToken always means "forward"
        // and PreviousToken always means "backward", regardless of query direction.
        var nextVal = storageResult.NextToken?.Value ?? string.Empty;
        var prevVal = storageResult.PreviousToken?.Value ?? string.Empty;
        var resultsToken = $"{nextVal}|{prevVal}";

        // Determine HasPrevResults and HasNextResults.
        // HasMoreData indicates whether more items exist in the queried sort direction:
        // - Forward nav or initial: more pages ahead
        // - Backward nav: more pages behind (query used reversed sort)
        bool hasPrevResults;
        bool hasNextResults;
        if (filter?.ResultsToken == null)
        {
            // Initial query — no previous results
            hasPrevResults = false;
            hasNextResults = storageResult.HasMoreData;
        }
        else if (!filter.RequestPriorResults)
        {
            // Navigated forward — there are always previous results
            hasPrevResults = true;
            hasNextResults = storageResult.HasMoreData;
        }
        else
        {
            // Navigated backward — more data in the reversed sort means more pages behind
            hasPrevResults = storageResult.HasMoreData;
            // We navigated backward from a later page, so there are always next results
            hasNextResults = true;
        }

        return new QueryResult<ServerSideSession>
        {
            ResultsToken = resultsToken,
            HasPrevResults = hasPrevResults,
            HasNextResults = hasNextResults,
            TotalCount = storageResult.TotalCount,
            TotalPages = storageResult.TotalPages,
            CurrentPage = null,
            Results = storageResult.Items.Select(ServerSideSessionRepository.DsoToModel).ToArray()
        };
    }
}
