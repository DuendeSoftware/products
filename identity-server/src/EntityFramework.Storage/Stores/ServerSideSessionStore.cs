// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Data.Common;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of IUserSessionStore thats uses EF.
/// </summary>
/// <seealso cref="IServerSideSessionStore" />
public class ServerSideSessionStore : IServerSideSessionStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IPersistedGrantDbContext Context;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<ServerSideSessionStore> Logger;

    /// <summary>
    /// The time provider.
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerSideSessionStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <exception cref="ArgumentNullException">context</exception>
    public ServerSideSessionStore(IPersistedGrantDbContext context, ILogger<ServerSideSessionStore> logger, TimeProvider timeProvider)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        TimeProvider = timeProvider;
    }



    /// <inheritdoc/>
    public virtual async Task CreateSessionAsync(ServerSideSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.CreateSession");

        var entity = new Entities.ServerSideSession
        {
            Key = session.Key,
            Scheme = session.Scheme,
            SessionId = session.SessionId,
            SubjectId = session.SubjectId,
            DisplayName = session.DisplayName,
            Created = session.Created,
            Renewed = session.Renewed,
            Expires = session.Expires,
            Data = session.Ticket,
        };
        Context.ServerSideSessions.Add(entity);

        try
        {
            await Context.SaveChangesAsync(ct);
            Log.CreatedNewServerSideSessionValueInDatabase(Logger, session.Key);
        }
        catch (DbUpdateException ex)
        {
            Log.ExceptionCreatingNewServerSideSessionInDatabase(Logger, ex.Message);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<ServerSideSession> GetSessionAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetSession");

        var entity = (await Context.ServerSideSessions.AsNoTracking().Where(x => x.Key == key)
            .ToArrayAsync(ct))
            .SingleOrDefault(x => x.Key == key);

        var model = default(ServerSideSession);
        if (entity != null)
        {
            model = new ServerSideSession
            {
                Key = entity.Key,
                Scheme = entity.Scheme,
                SubjectId = entity.SubjectId,
                SessionId = entity.SessionId,
                DisplayName = entity.DisplayName,
                Created = entity.Created,
                Renewed = entity.Renewed,
                Expires = entity.Expires,
                Ticket = entity.Data,
                Version = entity.Version,
            };
        }

        var found = model != null;
        Log.FoundServerSideSessionValueInDatabaseValue(Logger, key, found);

        return model;
    }

    /// <inheritdoc/>
    public virtual async Task<bool> UpdateSessionAsync(ServerSideSession session, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.UpdateSession");

        var entity = (await Context.ServerSideSessions.Where(x => x.Key == session.Key)
                .ToArrayAsync(ct))
            .SingleOrDefault(x => x.Key == session.Key);

        if (entity == null)
        {
            Log.NoServerSideSessionValueFoundInDatabase(Logger, session.Key);
            return false;
        }

        if (session.Version != 0 && session.Version != entity.Version)
        {
            return false;
        }

        entity.Scheme = session.Scheme;
        entity.SubjectId = session.SubjectId;
        entity.SessionId = session.SessionId;
        entity.DisplayName = session.DisplayName;
        entity.Created = session.Created;
        entity.Renewed = session.Renewed;
        entity.Expires = session.Expires;
        entity.Data = session.Ticket;
        entity.Version++;

        try
        {
            await Context.SaveChangesAsync(ct);
            Log.UpdatedServerSideSessionValueInDatabase(Logger, session.Key);
            return true;
        }
        catch (DbUpdateException ex)
        {
            Log.ExceptionUpdatingExistingServerSideSessionValueIn(Logger, session.Key, ex.Message);

            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public virtual async Task DeleteSessionAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.DeleteSession");

        var entity = (await Context.ServerSideSessions.Where(x => x.Key == key)
                        .ToArrayAsync(ct))
                    .SingleOrDefault(x => x.Key == key);

        if (entity == null)
        {
            Log.NoServerSideSessionValueFoundInDatabase2(Logger, key);
            return;
        }

        try
        {
            await Context.ServerSideSessions.Where(x => x.Id == entity.Id).ExecuteDeleteAsync(ct);
            Log.DeletedServerSideSessionValueInDatabase(Logger, key);
        }
        catch (DbException ex)
        {
            Log.ExceptionDeletingServerSideSessionValueInDatabase(Logger, key, ex.Message);
        }
    }



    /// <inheritdoc/>
    public virtual async Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetSessions");

        filter.Validate();

        var entities = await Filter(Context.ServerSideSessions.AsNoTracking().AsQueryable(), filter)
            .ToArrayAsync(ct);
        entities = Filter(entities.AsQueryable(), filter).ToArray();

        var results = entities.Select(entity => new ServerSideSession
        {
            Key = entity.Key,
            Scheme = entity.Scheme,
            SubjectId = entity.SubjectId,
            SessionId = entity.SessionId,
            DisplayName = entity.DisplayName,
            Created = entity.Created,
            Renewed = entity.Renewed,
            Expires = entity.Expires,
            Ticket = entity.Data,
            Version = entity.Version,
        }).ToArray();

        Log.FoundValueServerSideSessionsForValue(Logger, results.Length, filter);

        return results;
    }

    /// <inheritdoc/>
    public virtual async Task DeleteSessionsAsync(SessionFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.DeleteSessions");

        filter.Validate();

        var entities = await Filter(Context.ServerSideSessions.AsQueryable(), filter)
            .ToArrayAsync(ct);
        entities = Filter(entities.AsQueryable(), filter).ToArray();

        var sessionRowIdsToDelete = entities.Select(x => x.Id).ToArray();

        try
        {
            await Context.ServerSideSessions.Where(x => sessionRowIdsToDelete.Contains(x.Id)).ExecuteDeleteAsync(ct);
            Log.RemovedValueServerSideSessionsFromDatabaseFor(Logger, entities.Length, filter);
        }
        catch (DbException ex)
        {
            Log.ErrorRemovingValueServerSideSessionsFromDatabase(Logger, entities.Length, filter, ex.Message);
        }
    }

    private static IQueryable<Entities.ServerSideSession> Filter(IQueryable<Entities.ServerSideSession> query, SessionFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        return query;
    }


    /// <inheritdoc/>
    public virtual async Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetAndRemoveExpiredSessions");

        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var entities = await Context.ServerSideSessions
                            .AsNoTracking()
                            .Where(x => x.Expires < now)
                            .OrderBy(x => x.Id)
                            .Take(count)
                            .ToArrayAsync(ct);

        // The deletes run in one transaction, so a failure part way through deletes nothing: a session that is
        // deleted but not returned would never get its expiration processing (logout, token revocation).
        // A transaction the caller already started is used as is; otherwise the execution strategy runs it,
        // because a retrying strategy (EnableRetryOnFailure) refuses a transaction started outside it.
        var database = ((DbContext)Context).Database;
        var removed = database.CurrentTransaction != null
            ? await DeleteStillExpiredAsync(entities, now, ct)
            : await database.CreateExecutionStrategy().ExecuteAsync(async (Ct attemptCt) =>
            {
                await using var tx = await database.BeginTransactionAsync(attemptCt);
                var deleted = await DeleteStillExpiredAsync(entities, now, attemptCt);
                await tx.CommitAsync(attemptCt);
                return deleted;
            }, ct);

        if (entities.Length > 0)
        {
            Log.FoundAndRemovedValueExpiredServerSideSessions(Logger, removed.Count);
        }

        var results = removed.Select(entity => new ServerSideSession
        {
            Key = entity.Key,
            Scheme = entity.Scheme,
            SubjectId = entity.SubjectId,
            SessionId = entity.SessionId,
            DisplayName = entity.DisplayName,
            Created = entity.Created,
            Renewed = entity.Renewed,
            Expires = entity.Expires,
            Ticket = entity.Data,
        }).ToArray();

        return results;
    }

    // Deletes each session only while it is still expired, so a session renewed since it was selected stays,
    // and returns the sessions it deleted.
    private async Task<List<Entities.ServerSideSession>> DeleteStillExpiredAsync(Entities.ServerSideSession[] entities, DateTime now, Ct ct)
    {
        var removed = new List<Entities.ServerSideSession>(entities.Length);
        foreach (var entity in entities)
        {
            var deleted = await Context.ServerSideSessions
                .Where(x => x.Id == entity.Id && x.Expires < now)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                removed.Add(entity);
            }
        }

        return removed;
    }

    /// <inheritdoc/>
    public virtual async Task<QueryResult<ServerSideSession>> QuerySessionsAsync(Ct ct, SessionQuery filter = null)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.QuerySessions");

        // it's possible that this implementation could have been done differently (e.g. use the page number for the token)
        // but it was done deliberately in such a way to allow document databases to mimic the logic
        // and omit features not supported (such as total count, total pages, and current page)
        // given that this is intended to be used as an administrative UI feature, performance was less of a concern

        filter ??= new();

        var query = Context.ServerSideSessions.AsNoTracking().AsQueryable();
        query = ApplyFilter(query, filter);

        var (first, last) = ParseResultsToken(filter);
        var countRequested = filter.CountRequested;
        if (countRequested <= 0)
        {
            countRequested = 25;
        }

        var totalCount = await query.CountAsync(ct);
        var pagination = new SessionPaginationContext
        {
            CountRequested = countRequested,
            TotalCount = totalCount,
            TotalPages = (int)Math.Max(1, Math.Ceiling(totalCount / (countRequested * 1.0))),
            First = first,
            Last = last,
        };

        if (filter.RequestPriorResults)
        {
            await PreviousPage(query, first, pagination, ct);

            if (AtStartWithDeletedItems(pagination))
            {
                // this handles when we went back and are now at the beginning but items were deleted.
                // we need to start over and re-query from the beginning.
                filter.ResultsToken = null;
                filter.RequestPriorResults = false;
                return await QuerySessionsAsync(ct, filter);
            }
        }
        else
        {
            await NextPage(query, last, pagination, ct);
        }

        // this handles prior entries being deleted since paging begun
        if (pagination.CurrentPage <= 1)
        {
            pagination.CurrentPage = 1;
            pagination.HasPrev = false;
        }

        string resultsToken = null;
        if (pagination.Items.Length > 0)
        {
            resultsToken = $"{pagination.Items[0].Id},{pagination.Items[pagination.Items.Length - 1].Id}";
        }
        else
        {
            // no results, so we're out of bounds
            pagination.HasPrev = false;
            pagination.HasNext = false;
            pagination.TotalCount = 0;
            pagination.TotalPages = 0;
            pagination.CurrentPage = 0;
        }

        var models = MapEntitiesToModels(pagination.Items);

        var result = new QueryResult<ServerSideSession>
        {
            ResultsToken = resultsToken,
            HasNextResults = pagination.HasNext,
            HasPrevResults = pagination.HasPrev,
            TotalCount = pagination.TotalCount,
            TotalPages = pagination.TotalPages,
            CurrentPage = pagination.CurrentPage,
            Results = models
        };

        Log.FoundValueServerSideSessionsInDatabase(Logger, models.Length);

        return result;
    }

    private static bool AtStartWithDeletedItems(SessionPaginationContext pagination) => pagination.CurrentPage == 1 && pagination.HasNext && pagination.Items.Length < pagination.CountRequested;

    private static ServerSideSession[] MapEntitiesToModels(Entities.ServerSideSession[] items) => items.Select(entity => new ServerSideSession
    {
        Key = entity.Key,
        Scheme = entity.Scheme,
        SubjectId = entity.SubjectId,
        SessionId = entity.SessionId,
        DisplayName = entity.DisplayName,
        Created = entity.Created,
        Renewed = entity.Renewed,
        Expires = entity.Expires,
        Ticket = entity.Data,
    }).ToArray();

    private static async Task NextPage(IQueryable<Entities.ServerSideSession> query, int last, SessionPaginationContext pagination, Ct ct)
    {
        pagination.Items = await query.OrderBy(x => x.Id)
            // if lastResultsId is zero, then this will just start at beginning
            .Where(x => x.Id > last)
            // and we +1 to see if there's a next page
            .Take(pagination.CountRequested + 1)
            .ToArrayAsync(ct);

        // if we have the one extra, we have a next page
        pagination.HasNext = pagination.Items.Length > pagination.CountRequested;

        if (pagination.HasNext)
        {
            // omit next results entry
            pagination.Items = pagination.Items.SkipLast(1).ToArray();
        }

        // how many are to the left of these results?
        if (pagination.Items.Length > 0)
        {
            var priorCountId = pagination.Items[0].Id;
            var priorCount = await query.CountAsync(x => x.Id < last, ct);
            pagination.HasPrev = priorCount > 0;
            pagination.CurrentPage = 1 + (int)Math.Ceiling((1.0 * priorCount) / pagination.CountRequested);
        }
    }

    private static async Task PreviousPage(IQueryable<Entities.ServerSideSession> query, int first, SessionPaginationContext pagination, Ct ct)
    {
        // sets query at the prior record from the last results, but in reverse order
        pagination.Items = await query.OrderByDescending(x => x.Id)
            .Where(x => x.Id < first)
            // and we +1 to see if there's a prev page
            .Take(pagination.CountRequested + 1)
            .ToArrayAsync(ct);

        // put them back into ID order
        pagination.Items = pagination.Items.OrderBy(x => x.Id).ToArray();

        // if we have the one extra, we have a prev page
        pagination.HasPrev = pagination.Items.Length > pagination.CountRequested;

        if (pagination.HasPrev)
        {
            // omit prev results entry
            pagination.Items = pagination.Items.Skip(1).ToArray();
        }

        // how many are to the right of these results?
        if (pagination.Items.Length > 0)
        {
            var postCountId = pagination.Items[pagination.Items.Length - 1].Id;
            var postCount = await query.CountAsync(x => x.Id > postCountId, ct);
            pagination.HasNext = postCount > 0;
            pagination.CurrentPage = pagination.TotalPages - (int)Math.Ceiling((1.0 * postCount) / pagination.CountRequested);
        }
    }

    private static (int First, int Last) ParseResultsToken(SessionQuery filter)
    {
        // these are the ids of first and last items in the prior results
        // stored as "x,y" in the filter.ResultsToken.
        var first = 0;
        var last = 0;
        if (filter.ResultsToken != null)
        {
            var parts = filter.ResultsToken.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts != null && parts.Length == 2)
            {
                _ = int.TryParse(parts[0], out first);
                _ = int.TryParse(parts[1], out last);
            }
        }

        return (first, last);
    }

    /// <summary>
    /// Applies the SessionQuery filter. The base implementation filters by
    /// DisplayName, sub, and sid, and if more than one criteria exist on the
    /// filter, they must all be fulfilled. This method (or an override of it)
    /// is not intended to apply pagination.
    /// </summary>
    protected virtual IQueryable<Entities.ServerSideSession> ApplyFilter(IQueryable<Entities.ServerSideSession> query, SessionQuery filter)
    {
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
        return query;
    }

    private sealed class SessionPaginationContext
    {
        public int TotalCount { get; set; }
        public int CountRequested { get; set; }
        public int TotalPages { get; set; }
        public int First { get; init; }
        public int Last { get; init; }
        public int CurrentPage { get; set; } = 1;
        public bool HasNext { get; set; }
        public bool HasPrev { get; set; }
        public Entities.ServerSideSession[] Items { get; set; } = Array.Empty<Entities.ServerSideSession>();
    }
}
