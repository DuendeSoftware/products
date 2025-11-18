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
    ILogger<InMemoryUserSessionStore> logger) : IUserSessionStore
{
    // A shorthand for the concurrent dictionary of user sessions, keyed by session key.
    private class UserSessionDictionary : ConcurrentDictionary<UserKey, UserSession>;

    // A dictionary of dictionaries, where the outer dictionary is keyed by partition key
    private readonly ConcurrentDictionary<PartitionKey, UserSessionDictionary> _store = new();

    public Task CreateUserSessionAsync(UserSession session, CT ct = default)
    {
        if (!session.PartitionKey.HasValue)
        {
            throw new InvalidOperationException(nameof(session.PartitionKey) + " cannot be null");
        }

        if (!session.Key.HasValue)
        {
            throw new InvalidOperationException(nameof(session.Key));
        }

        var partition = GetPartition(session.PartitionKey.Value);
        if (!partition.TryAdd(session.Key.Value, session.Clone()))
        {
            // There is a known race condition when two requests are trying to create a session at the same time.
            logger.DuplicateSessionInsertDetected(LogLevel.Information);
        }

        return Task.CompletedTask;
    }

    private UserSessionDictionary GetPartition(PartitionKey key)
    {
        var partition = _store.GetOrAdd(key, _ => new UserSessionDictionary());
        return partition;
    }

    public Task<UserSession?> GetUserSessionAsync(UserSessionKey key, CT ct = default)
    {
        var partition = GetPartition(key.PartitionKey);
        partition.TryGetValue(key.UserKey, out var item);

        return Task.FromResult(item?.Clone());
    }

    public Task UpdateUserSessionAsync(UserSessionKey key, UserSessionUpdate session, CT ct = default)
    {
        var partition = GetPartition(key.PartitionKey);
        if (!partition.TryGetValue(key.UserKey, out var existing))
        {
            return Task.CompletedTask;
        }

        var item = existing.Clone();
        session.CopyTo(item);
        partition[key.UserKey] = item;

        return Task.CompletedTask;
    }

    public Task DeleteUserSessionAsync(UserSessionKey key, CT ct = default)
    {
        var partition = GetPartition(key.PartitionKey);
        partition.TryRemove(key.UserKey, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<UserSession>> GetUserSessionsAsync(PartitionKey partitionKey, UserSessionsFilter filter, CT ct = default)
    {
        filter.Validate();
        var partition = GetPartition(partitionKey);

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

    public Task DeleteUserSessionsAsync(PartitionKey partitionKey, UserSessionsFilter filter, CT ct = default)
    {
        filter.Validate();
        var partition = GetPartition(partitionKey);

        var query = partition.Values.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var keys = query.Select(x => x.Key!.Value)
            .ToArray();
        foreach (var key in keys)
        {
            partition.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
