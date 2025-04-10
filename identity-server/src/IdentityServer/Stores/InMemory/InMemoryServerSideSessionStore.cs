// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Concurrent;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory user session store
/// </summary>
public class InMemoryServerSideSessionStore : IServerSideSessionStore
{
    private readonly ConcurrentDictionary<string, ServerSideSession> _store = new();



    /// <inheritdoc />
    public Task CreateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.CreateSession");

        if (!_store.TryAdd(session.Key, session.Clone()))
        {
            throw new Exception("Key already exists");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ServerSideSession> GetSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.GetSession");

        _store.TryGetValue(key, out var item);
        return Task.FromResult(item?.Clone());
    }

    /// <inheritdoc />
    public Task UpdateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.UpdateSession");

        _store[session.Key] = session.Clone();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.DeleteSession");

        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }



    /// <inheritdoc />
    public Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.GetSessions");

        filter.Validate();

        var query = _store.Values.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var results = query.Select(x => x.Clone()).ToArray();
        return Task.FromResult((IReadOnlyCollection<ServerSideSession>)results);
    }

    /// <inheritdoc />
    public Task DeleteSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.DeleteSessions");

        filter.Validate();

        var query = _store.Values.AsQueryable();
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
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }


    /// <inheritdoc/>
    public Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.GetAndRemoveExpiredSession");

        var results = _store.Values
            .Where(x => x.Expires < DateTime.UtcNow)
            .OrderBy(x => x.Key)
            .Take(count)
            .ToArray();

        foreach (var item in results)
        {
            _store.Remove(item.Key, out _);
        }

        return Task.FromResult((IReadOnlyCollection<ServerSideSession>)results);
    }



    /// <inheritdoc/>
    public Task<QueryResult<ServerSideSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryServerSideSessionStore.QuerySessions");

        // it's possible that this implementation could have been done differently (e.g. use the page number for the token)
        // but it was done deliberatly in such a way to allow document databases to mimic the logic
        // and omit features not supported (such as total count, total pages, and current page)
        // given that this is intended to be used as an administrative UI feature, performance was less of a concern

        filter ??= new();

        // these are the keys of first and last items in the prior results
        // stored as "x,y" in the filter.ResultsToken.
        var first = string.Empty;
        var last = string.Empty;

        if (filter.ResultsToken != null)
        {
            var parts = filter.ResultsToken.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts != null && parts.Length == 2)
            {
                first = parts[0];
                last = parts[1];
            }
        }

        var countRequested = filter.CountRequested;
        if (countRequested <= 0) countRequested = 25;

        var query = _store.Values.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.DisplayName) ||
            !string.IsNullOrWhiteSpace(filter.SubjectId) ||
            !string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x =>
                (filter.SubjectId == null || x.SubjectId.Contains(filter.SubjectId)) &&
                (filter.SessionId == null || x.SessionId.Contains(filter.SessionId)) &&
                (filter.DisplayName == null || (x.DisplayName != null && x.DisplayName.Contains(filter.DisplayName) == true))
            );
        }

        var totalCount = query.Count();
        var totalPages = (int)Math.Max(1, Math.Ceiling(totalCount / (countRequested * 1.0)));

        var currPage = 1;

        var hasNext = false;
        var hasPrev = false;
        ServerSideSession[] items;

        if (filter.RequestPriorResults)
        {
            // sets query at the prior record from the last results, but in reverse order
            items = query.OrderByDescending(x => x.Key)
                .Where(x => string.Compare(x.Key, first) < 0)
                // and we +1 to see if there's a prev page
                .Take(countRequested + 1)
                .ToArray();

            // put them back into ID order
            items = items.OrderBy(x => x.Key).ToArray();

            // if we have the one extra, we have a prev page
            hasPrev = items.Length > countRequested;

            if (hasPrev)
            {
                // omit prev results entry
                items = items.Skip(1).ToArray();
            }

            // how many are to the right of these results?
            if (items.Any())
            {
                var postCountId = items[items.Length - 1].Key;
                var postCount = query.Where(x => string.Compare(x.Key, postCountId) > 0).Count();
                hasNext = postCount > 0;
                currPage = totalPages - (int)Math.Ceiling((1.0 * postCount) / countRequested);
            }

            if (currPage == 1 && hasNext && items.Length < countRequested)
            {
                // this handles when we went back and are now at the beginning but items were deleted.
                // we need to start over and re-query from the beginning.
                filter.ResultsToken = null;
                filter.RequestPriorResults = false;
                return QuerySessionsAsync(filter);
            }
        }
        else
        {
            items = query.OrderBy(x => x.Key)
                // if last is "", then this will just start at beginning
                .Where(x => string.Compare(x.Key, last) > 0)
                // and we +1 to see if there's a next page
                .Take(countRequested + 1)
                .ToArray();

            // if we have the one extra, we have a next page
            hasNext = items.Length > countRequested;

            if (hasNext)
            {
                // omit next results entry
                items = items.SkipLast(1).ToArray();
            }

            // how many are to the left of these results?
            if (items.Any())
            {
                var priorCountId = items[0].Key;
                var priorCount = query.Where(x => string.Compare(x.Key, priorCountId) < 0).Count();
                hasPrev = priorCount > 0;
                currPage = 1 + (int)Math.Ceiling((1.0 * priorCount) / countRequested);
            }
        }

        // this handles prior entries being deleted since paging begun
        if (currPage <= 1)
        {
            currPage = 1;
            hasPrev = false;
        }

        string resultsToken = null;
        if (items.Length > 0)
        {
            resultsToken = $"{items[0].Key},{items[items.Length - 1].Key}";
        }
        else
        {
            hasPrev = false;
            hasNext = false;
            totalCount = 0;
            totalPages = 0;
            currPage = 0;
        }

        var results = items.Select(entity => entity.Clone()).ToArray();

        var result = new QueryResult<ServerSideSession>
        {
            ResultsToken = resultsToken,
            HasNextResults = hasNext,
            HasPrevResults = hasPrev,
            TotalCount = totalCount,
            TotalPages = totalPages,
            CurrentPage = currPage,
            Results = results
        };

        return Task.FromResult(result);
    }
}
