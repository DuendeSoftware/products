// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Duende.Bff.EntityFramework;

/// <summary>
/// Entity framework core implementation of IUserSessionStore
/// </summary>
public class UserSessionStore : IUserSessionStore, IUserSessionStoreCleanup
{
    private readonly string _applicationDiscriminator;
    private readonly ISessionDbContext _sessionDbContext;
    private readonly ILogger<UserSessionStore> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="sessionDbContext"></param>
    /// <param name="logger"></param>
    public UserSessionStore(IOptions<DataProtectionOptions> options, ISessionDbContext sessionDbContext, ILogger<UserSessionStore> logger)
    {
        _applicationDiscriminator = options.Value.ApplicationDiscriminator;
        _sessionDbContext = sessionDbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CreateUserSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Creating user session record in store for sub {sub} sid {sid}", session.SubjectId, session.SessionId);

        var item = new UserSessionEntity()
        {
            ApplicationName = _applicationDiscriminator
        };
        session.CopyTo(item);
        _sessionDbContext.UserSessions.Add(item);

        try
        {
            await _sessionDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning("Exception creating new server-side session in database: {error}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteUserSessionAsync(string key, CancellationToken cancellationToken)
    {
        var items = await _sessionDbContext.UserSessions.Where(x => x.Key == key && x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        var item = items.SingleOrDefault(x => x.Key == key && x.ApplicationName == _applicationDiscriminator);

        if (item != null)
        {
            _logger.LogDebug("Deleting user session record in store for sub {sub} sid {sid}", item.SubjectId, item.SessionId);

            _sessionDbContext.UserSessions.Remove(item);
            try
            {
                await _sessionDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // suppressing exception for concurrent deletes
                // https://github.com/DuendeSoftware/BFF/issues/63
                _logger.LogDebug("DbUpdateConcurrencyException: {error}", ex.Message);

                foreach (var entry in ex.Entries)
                {
                    // mark detatched so another call to SaveChangesAsync won't throw again
                    entry.State = EntityState.Detached;
                }
            }
        }
        else
        {
            _logger.LogDebug("No record found in user session store when trying to delete user session for key {key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken)
    {
        filter.Validate();

        var query = _sessionDbContext.UserSessions.Where(x => x.ApplicationName == _applicationDiscriminator).AsQueryable();
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var items = await query.Where(x => x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            items = items.Where(x => x.SubjectId == filter.SubjectId).ToArray();
        }
        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            items = items.Where(x => x.SessionId == filter.SessionId).ToArray();
        }

        _logger.LogDebug("Deleting {count} user session(s) from store for sub {sub} sid {sid}", items.Length, filter.SubjectId, filter.SessionId);

        _sessionDbContext.UserSessions.RemoveRange(items);

        try
        {
            await _sessionDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // suppressing exception for concurrent deletes
            // https://github.com/DuendeSoftware/BFF/issues/63
            _logger.LogDebug("DbUpdateConcurrencyException: {error}", ex.Message);

            foreach (var entry in ex.Entries)
            {
                // mark detatched so another call to SaveChangesAsync won't throw again
                entry.State = EntityState.Detached;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<UserSession> GetUserSessionAsync(string key, CancellationToken cancellationToken)
    {
        var items = await _sessionDbContext.UserSessions.Where(x => x.Key == key && x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        var item = items.SingleOrDefault(x => x.Key == key && x.ApplicationName == _applicationDiscriminator);

        UserSession result = null;
        if (item != null)
        {
            _logger.LogDebug("Getting user session record from store for sub {sub} sid {sid}", item.SubjectId, item.SessionId);
            
            result = new UserSession();
            item.CopyTo(result);
        }
        else
        {
            _logger.LogDebug("No record found in user session store when trying to get user session for key {key}", key);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<UserSession>> GetUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken)
    {
        filter.Validate();

        var query = _sessionDbContext.UserSessions.Where(x => x.ApplicationName == _applicationDiscriminator).AsQueryable();
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        var items = await query.Where(x => x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            items = items.Where(x => x.SubjectId == filter.SubjectId).ToArray();
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            items = items.Where(x => x.SessionId == filter.SessionId).ToArray();
        }

        var results = items.Select(x =>
        {
            var item = new UserSession();
            x.CopyTo(item);
            return item;
        }).ToArray();

        _logger.LogDebug("Getting {count} user session(s) from store for sub {sub} sid {sid}", results.Length, filter.SubjectId, filter.SessionId);

        return results;
    }

    /// <inheritdoc/>
    public async Task UpdateUserSessionAsync(string key, UserSessionUpdate session, CancellationToken cancellationToken)
    {
        var items = await _sessionDbContext.UserSessions.Where(x => x.Key == key && x.ApplicationName == _applicationDiscriminator).ToArrayAsync(cancellationToken);
        var item = items.SingleOrDefault(x => x.Key == key && x.ApplicationName == _applicationDiscriminator);
        if (item != null)
        {
            _logger.LogDebug("Updating user session record in store for sub {sub} sid {sid}", item.SubjectId, item.SessionId);

            session.CopyTo(item);
            await _sessionDbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _logger.LogDebug("No record found in user session store when trying to update user session for key {key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var found = int.MaxValue;
        var batchSize = 100;

        while (found >= batchSize)
        {
            var expired = await _sessionDbContext.UserSessions
                .Where(x => x.Expires < DateTime.UtcNow)
                .OrderBy(x => x.Id)
                .Take(batchSize)
                .ToArrayAsync(cancellationToken);

            found = expired.Length;

            if (found > 0)
            {
                _logger.LogDebug("Removing {serverSideSessionCount} server side sessions", found);

                _sessionDbContext.UserSessions.RemoveRange(expired);

                try
                {
                    await _sessionDbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // suppressing exception for concurrent deletes
                    _logger.LogDebug("DbUpdateConcurrencyException: {error}", ex.Message);

                    foreach (var entry in ex.Entries)
                    {
                        // mark detatched so another call to SaveChangesAsync won't throw again
                        entry.State = EntityState.Detached;
                    }
                }
            }
        }
    }
}