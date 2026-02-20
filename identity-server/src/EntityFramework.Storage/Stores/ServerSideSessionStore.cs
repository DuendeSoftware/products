// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Extensions;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
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
    /// The CancellationToken provider.
    /// </summary>
    protected readonly ICancellationTokenProvider CancellationTokenProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<ServerSideSessionStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerSideSessionStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationTokenProvider"></param>
    /// <exception cref="ArgumentNullException">context</exception>
    public ServerSideSessionStore(IPersistedGrantDbContext context, ILogger<ServerSideSessionStore> logger, ICancellationTokenProvider cancellationTokenProvider)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        CancellationTokenProvider = cancellationTokenProvider;
    }



    /// <inheritdoc/>
    public virtual async Task CreateSessionAsync(ServerSideSession session, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.CreateSession");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

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
            Logger.LogDebug("Created new server-side session {serverSideSessionKey} in database", session.Key);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogWarning("Exception creating new server-side session in database: {error}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<ServerSideSession> GetSessionAsync(string key, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetSession");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

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
            };
        }

        Logger.LogDebug("Found server-side session {serverSideSessionKey} in database: {serverSideSessionKeyFound}", key, model != null);

        return model;
    }

    /// <inheritdoc/>
    public virtual async Task UpdateSessionAsync(ServerSideSession session, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.UpdateSession");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

        var entity = (await Context.ServerSideSessions.Where(x => x.Key == session.Key)
                .ToArrayAsync(ct))
            .SingleOrDefault(x => x.Key == session.Key);

        if (entity == null)
        {
            Logger.LogDebug("No server-side session {serverSideSessionKey} found in database. Update failed.", session.Key);
            return;
        }

        entity.Scheme = session.Scheme;
        entity.SubjectId = session.SubjectId;
        entity.SessionId = session.SessionId;
        entity.DisplayName = session.DisplayName;
        entity.Created = session.Created;
        entity.Renewed = session.Renewed;
        entity.Expires = session.Expires;
        entity.Data = session.Ticket;

        try
        {
            await Context.SaveChangesAsync(ct);
            Logger.LogDebug("Updated server-side session {serverSideSessionKey} in database", session.Key);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogWarning("Exception updating existing server side session {serverSideSessionKey} in database: {error}", session.Key, ex.Message);
        }
    }

    /// <inheritdoc/>
    public virtual async Task DeleteSessionAsync(string key, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.DeleteSession");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

        var entity = (await Context.ServerSideSessions.Where(x => x.Key == key)
                        .ToArrayAsync(ct))
                    .SingleOrDefault(x => x.Key == key);

        if (entity == null)
        {
            Logger.LogDebug("No server side session {serverSideSessionKey} found in database. Delete failed.", key);
            return;
        }

        Context.ServerSideSessions.Remove(entity);

        try
        {
            await Context.SaveChangesAsync(ct);
            Logger.LogDebug("Deleted server-side session {serverSideSessionKey} in database", key);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogWarning("Exception deleting server-side session {serverSideSessionKey} in database: {error}", key, ex.Message);
        }
    }



    /// <inheritdoc/>
    public virtual async Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetSessions");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

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
        }).ToArray();

        Logger.LogDebug("Found {serverSideSessionCount} server-side sessions for {@filter}", results.Length, filter);

        return results;
    }

    /// <inheritdoc/>
    public virtual async Task DeleteSessionsAsync(SessionFilter filter, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.DeleteSessions");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

        filter.Validate();

        var entities = await Filter(Context.ServerSideSessions.AsQueryable(), filter)
            .ToArrayAsync(ct);
        entities = Filter(entities.AsQueryable(), filter).ToArray();

        Context.ServerSideSessions.RemoveRange(entities);

        try
        {
            await Context.SaveChangesAsync(ct);
            Logger.LogDebug("Removed {serverSideSessionCount} server-side sessions from database for {@filter}", entities.Length, filter);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogInformation("Error removing {serverSideSessionCount} server-side sessions from database for {@filter}: {error}", entities.Length, filter, ex.Message);
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
    public virtual async Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.GetAndRemoveExpiredSessions");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

        var entities = await Context.ServerSideSessions
                            .Where(x => x.Expires < DateTime.UtcNow)
                            .OrderBy(x => x.Id)
                            .Take(count)
                            .ToArrayAsync(ct);

        if (entities.Length > 0)
        {
            Context.ServerSideSessions.RemoveRange(entities);

            var list = await Context.SaveChangesWithConcurrencyCheckAsync<Entities.ServerSideSession>(Logger, ct);
            entities = entities.Except(list).ToArray();

            Logger.LogDebug("Found and removed {serverSideSessionCount} expired server-side sessions", entities.Length);
        }

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
        }).ToArray();

        return results;
    }

    /// <inheritdoc/>
    public virtual async Task<QueryResult<ServerSideSession>> QuerySessionsAsync(SessionQuery filter = null, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideSessionStore.QuerySessions");

        ct = ct == CT.None ? CancellationTokenProvider.CancellationToken : ct;

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
                return await QuerySessionsAsync(filter, ct);
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

        Logger.LogDebug("Found {serverSideSessionCount} server-side sessions in database", models.Length);

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

    private static async Task NextPage(IQueryable<Entities.ServerSideSession> query, int last, SessionPaginationContext pagination, CT ct)
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

    private static async Task PreviousPage(IQueryable<Entities.ServerSideSession> query, int first, SessionPaginationContext pagination, CT ct)
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
