// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Duende.Bff;

/// <summary>
/// In-memory user session store
/// </summary>
public class InMemoryUserSessionStore : IUserSessionStore
{
    private readonly ConcurrentDictionary<string, UserSession> _store = new();

    /// <inheritdoc />
    public Task CreateUserSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        if (!_store.TryAdd(session.Key, session.Clone()))
        {
            throw new Exception("Key already exists");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<UserSession?> GetUserSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(key, out var item);
        return Task.FromResult(item?.Clone());
    }

    /// <inheritdoc />
    public Task UpdateUserSessionAsync(string key, UserSessionUpdate session, CancellationToken cancellationToken = default)
    {
        var item = _store[key].Clone();
        session.CopyTo(item);
        _store[key] = item;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteUserSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
        
    /// <inheritdoc />
    public Task<IReadOnlyCollection<UserSession>> GetUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Validate();

        var query = _store.Values.AsQueryable();
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var results = query.Select(x => x.Clone()).ToArray();
        return Task.FromResult((IReadOnlyCollection<UserSession>)results);
    }

    /// <inheritdoc />
    public Task DeleteUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Validate();

        var query = _store.Values.AsQueryable();
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var keys = query.Select(x => x.Key).ToArray();

        foreach(var key in keys)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}