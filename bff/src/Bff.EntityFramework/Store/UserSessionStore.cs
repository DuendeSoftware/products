// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Duende.Bff.EntityFramework;

/// <summary>
/// Entity framework core implementation of IUserSessionStore
/// </summary>
public class UserSessionStore(IOptions<DataProtectionOptions> options, ISessionDbContext sessionDbContext, ILogger<UserSessionStore> logger) : IUserSessionStore, IUserSessionStoreCleanup
{
    private readonly string? _applicationDiscriminator = options.Value.ApplicationDiscriminator;

    /// <inheritdoc/>
    public async Task CreateUserSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating user session record in store for sub {sub} sid {sid}", session.SubjectId, session.SessionId);

        var item = new UserSessionEntity()
        {
            ApplicationName = _applicationDiscriminator
        };
        session.CopyTo(item);
        sessionDbContext.UserSessions.Add(item);

        try
        {
            await sessionDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var exception = ex.ToString();

            // There is a known race condition when two requests are trying to create a session at the same time.
            // First, we delete the old session, then we insert the new session without the overhead of a transaction. 
            // It's safe to ignore this exception IF it's a unique exception. The problem is, how do you check for
            // unique constraint violations in a database-agnostic way? Here, we do that by looking at the exception message (ugh).

            // SQLite would send:  ---> Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 19: 'UNIQUE constraint failed: UserSessions.ApplicationName, UserSessions.SessionId'.
            // SQL Server would send:  ---> Microsoft.Data.SqlClient.SqlException (0x80131904): Cannot insert duplicate key row in object 'Session.UserSessions' with unique index 'IX_UserSessions_ApplicationName_SessionId'. The duplicate key value is (<AppName>, <SessionIdValue>).
            // Postgres would send:  ---> Npgsql.PostgresException (0x80004005): 23505: duplicate key value violates unique constraint "IX_UserSessions_ApplicationName_SessionId"
            // MySQL would send:    ---> MySql.Data.MySqlClient.MySqlException (0x80004005): Duplicate entry '<AppName>-<SessionIdValue>' for key 'IX_UserSessions_ApplicationName_SessionId'
            if (exception.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) || exception.Contains("IX_UserSessions_ApplicationName_SessionId"))
            {
                logger.LogDebug(ex, "Detected a duplicate insert of the same session. This can happen when multiple browser tabs are open and can safely be ignored.");
            }
            else
            {
                logger.LogWarning(ex, "Exception creating new server-side session in database: {error}. If this is a duplicate key error, it's safe to ignore. This can happen (for example) when two identical tabs are open.", ex.Message);
            }
        }
    }

    /// <inheritdoc/>
    public async Task DeleteUserSessionAsync(string key, CancellationToken cancellationToken)
    {
        var items = await sessionDbContext.UserSessions.Where(x => x.Key == key && x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        var item = items.SingleOrDefault(x => x.Key == key && x.ApplicationName == _applicationDiscriminator);

        if (item == null)
        {
            logger.LogDebug("No record found in user session store when trying to delete user session for key {key}",
                key);
            return;
        }

        logger.LogDebug("Deleting user session record in store for sub {sub} sid {sid}", item.SubjectId,
            item.SessionId);

        sessionDbContext.UserSessions.Remove(item);
        try
        {
            await sessionDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // suppressing exception for concurrent deletes
            // https://github.com/DuendeSoftware/BFF/issues/63
            logger.LogDebug("DbUpdateConcurrencyException: {error}", ex.Message);

            foreach (var entry in ex.Entries)
            {
                // mark detatched so another call to SaveChangesAsync won't throw again
                entry.State = EntityState.Detached;
            }
        }
    }

    /// <inheritdoc/>
    public async Task DeleteUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken)
    {
        filter.Validate();

        var query = sessionDbContext.UserSessions.Where(x => x.ApplicationName == _applicationDiscriminator).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var items = await query.Where(x => x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            items = items.Where(x => x.SubjectId == filter.SubjectId).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            items = items.Where(x => x.SessionId == filter.SessionId).ToArray();
        }

        logger.LogDebug("Deleting {count} user session(s) from store for sub {sub} sid {sid}", items.Length, filter.SubjectId, filter.SessionId);

        sessionDbContext.UserSessions.RemoveRange(items);

        try
        {
            await sessionDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // suppressing exception for concurrent deletes
            // https://github.com/DuendeSoftware/BFF/issues/63
            logger.LogDebug("DbUpdateConcurrencyException: {error}", ex.Message);

            foreach (var entry in ex.Entries)
            {
                // mark detatched so another call to SaveChangesAsync won't throw again
                entry.State = EntityState.Detached;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<UserSession?> GetUserSessionAsync(string key, CancellationToken cancellationToken)
    {
        var items = await sessionDbContext.UserSessions.Where(x => x.Key == key && x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        var item = items.SingleOrDefault(x => x.Key == key && x.ApplicationName == _applicationDiscriminator);

        UserSession? result = null;
        if (item == null)
        {
            logger.LogDebug("No record found in user session store when trying to get user session for key {key}", key);
            return null;
        }

        logger.LogDebug("Getting user session record from store for sub {sub} sid {sid}", item.SubjectId,
            item.SessionId);

        result = new UserSession();
        item.CopyTo(result);

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<UserSession>> GetUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken)
    {
        filter.Validate();

        var query = sessionDbContext.UserSessions.Where(x => x.ApplicationName == _applicationDiscriminator).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var items = await query.Where(x => x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            items = items.Where(x => x.SubjectId == filter.SubjectId).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            items = items.Where(x => x.SessionId == filter.SessionId).ToArray();
        }

        var results = items.Select(x =>
        {
            var item = new UserSession();
            x.CopyTo(item);
            return item;
        }).ToArray();

        logger.LogDebug("Getting {count} user session(s) from store for sub {sub} sid {sid}", results.Length, filter.SubjectId, filter.SessionId);

        return results;
    }

    /// <inheritdoc/>
    public async Task UpdateUserSessionAsync(string key, UserSessionUpdate session, CancellationToken cancellationToken)
    {
        var items = await sessionDbContext.UserSessions.Where(x => x.Key == key && x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        var item = items.SingleOrDefault(x => x.Key == key && x.ApplicationName == _applicationDiscriminator);
        if (item == null)
        {
            logger.LogDebug("No record found in user session store when trying to update user session for key {key}",
                key);
            return;
        }

        logger.LogDebug("Updating user session record in store for sub {sub} sid {sid}", item.SubjectId,
            item.SessionId);

        session.CopyTo(item);
        await sessionDbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var found = int.MaxValue;
        var batchSize = 100;

        while (found >= batchSize)
        {
            var expired = await sessionDbContext.UserSessions
                .Where(x => x.Expires < DateTime.UtcNow)
                .OrderBy(x => x.Id)
                .Take(batchSize)
                .ToArrayAsync(cancellationToken);

            found = expired.Length;

            if (found <= 0)
            {
                continue;
            }

            logger.LogDebug("Removing {serverSideSessionCount} server side sessions", found);

            sessionDbContext.UserSessions.RemoveRange(expired);

            try
            {
                await sessionDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // suppressing exception for concurrent deletes
                logger.LogDebug("DbUpdateConcurrencyException: {error}", ex.Message);

                foreach (var entry in ex.Entries)
                {
                    // mark detatched so another call to SaveChangesAsync won't throw again
                    entry.State = EntityState.Detached;
                }
            }
        }
    }
}
