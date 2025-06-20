// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.Bff.Otel;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// In-memory user session store partitioned by partition key
/// </summary>
internal class InMemoryUserSessionStore(
    IUserSessionPartitionKeyBuilder partitionKeyBuilder,
    ILogger<InMemoryUserSessionStore> logger) : IUserSessionStore
{
    // A shorthand for the concurrent dictionary of user sessions, keyed by session key.
    private class UserSessionDictionary : ConcurrentDictionary<string, UserSession>;

    // A dictionary of dictionaries, where the outer dictionary is keyed by partition key
    private readonly ConcurrentDictionary<string, UserSessionDictionary> _store = new();

    // the in-memory implementation requires a partition key to avoid issues with null values in
    // the concurrent dictionary. 
    private string PartitionKey => partitionKeyBuilder.BuildPartitionKey() ?? "[null]";

    public Task CreateUserSessionAsync(UserSession session, CT ct = default)
    {
        var partition = GetPartition();
        if (!partition.TryAdd(session.Key, session.Clone()))
        {
            // There is a known race condition when two requests are trying to create a session at the same time.
            logger.DuplicateSessionInsertDetected();
        }

        return Task.CompletedTask;
    }

    private UserSessionDictionary GetPartition()
    {
        var partitionKey = PartitionKey;
        var partition = _store.GetOrAdd(partitionKey, _ => new UserSessionDictionary());
        return partition;
    }

    public Task<UserSession?> GetUserSessionAsync(string key, CT ct = default)
    {
        var partition = GetPartition();
        partition.TryGetValue(key, out var item);

        return Task.FromResult(item?.Clone());
    }

    public Task UpdateUserSessionAsync(string key, UserSessionUpdate session, CT ct = default)
    {
        var partition = GetPartition();
        if (!partition.TryGetValue(key, out var existing))
        {
            return Task.CompletedTask;
        }

        var item = existing.Clone();
        session.CopyTo(item);
        partition[key] = item;

        return Task.CompletedTask;
    }

    public Task DeleteUserSessionAsync(string key, CT ct = default)
    {
        var partition = GetPartition();
        partition.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<UserSession>> GetUserSessionsAsync(UserSessionsFilter filter, CT ct = default)
    {
        filter.Validate();
        var partition = GetPartition();

        var query = partition.Values.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var results = query.Select(x => x.Clone()).ToArray();
        return Task.FromResult((IReadOnlyCollection<UserSession>)results);
    }

    public Task DeleteUserSessionsAsync(UserSessionsFilter filter, CT ct = default)
    {
        filter.Validate();
        var partition = GetPartition();

        var query = partition.Values.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var keys = query.Select(x => x.Key).ToArray();
        foreach (var key in keys)
        {
            partition.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
