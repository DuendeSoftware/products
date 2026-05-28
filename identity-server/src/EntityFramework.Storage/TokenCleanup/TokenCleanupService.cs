// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework;

/// <inheritdoc/>
public class TokenCleanupService : ITokenCleanupService
{
    private readonly OperationalStoreOptions _options;
    private readonly IPersistedGrantDbContext _persistedGrantDbContext;
    private readonly IOperationalStoreNotification _operationalStoreNotification;
    private readonly ILogger<TokenCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Constructor for TokenCleanupService.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="persistedGrantDbContext"></param>
    /// <param name="logger"></param>
    /// <param name="timeProvider"></param>
    /// <param name="operationalStoreNotification"></param>
    public TokenCleanupService(
        OperationalStoreOptions options,
        IPersistedGrantDbContext persistedGrantDbContext,
        ILogger<TokenCleanupService> logger,
        TimeProvider timeProvider,
        IOperationalStoreNotification operationalStoreNotification = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.TokenCleanupBatchSize < 1)
        {
            throw new ArgumentException("Token cleanup batch size interval must be at least 1");
        }

        _persistedGrantDbContext = persistedGrantDbContext ?? throw new ArgumentNullException(nameof(persistedGrantDbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        _operationalStoreNotification = operationalStoreNotification;
    }

    /// <inheritdoc/>
    public async Task CleanupGrantsAsync(Ct ct)
    {
        try
        {
            _logger.LogTrace("Querying for expired grants to remove");

            await RemoveGrantsAsync(ct);
            await RemoveDeviceCodesAsync(ct);
            await RemovePushedAuthorizationRequestsAsync(ct);
            await RemoveSamlSigninStatesAsync(ct);
            await RemoveSamlLogoutSessionsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception removing expired grants: {exception}", ex.Message);
        }
    }

    /// <summary>
    /// Removes the stale persisted grants.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task RemoveGrantsAsync(Ct ct)
    {
        await RemoveExpiredPersistedGrantsAsync(ct);
        if (_options.RemoveConsumedTokens)
        {
            await RemoveConsumedPersistedGrantsAsync(ct);
        }
    }

    /// <summary>
    /// Removes the expired persisted grants.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task RemoveExpiredPersistedGrantsAsync(Ct ct)
    {
        var found = int.MaxValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (_operationalStoreNotification == null)
        {
            var query = _persistedGrantDbContext.PersistedGrants
                .Where(x => x.Expiration < now)
                .OrderBy(x => x.Expiration);

            while (found >= _options.TokenCleanupBatchSize)
            {
                found = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .ExecuteDeleteAsync(ct);

                if (found > 0)
                {
                    _logger.LogInformation("Removed {grantCount} expired grants", found);
                }
            }
        }
        else
        {
            while (found >= _options.TokenCleanupBatchSize)
            {
                // Filter and order on expiration which is indexed, this allows the
                // DB engine to just take the first N items from the index
                var query = _persistedGrantDbContext.PersistedGrants
                    .Where(x => x.Expiration < now)
                    .OrderBy(x => x.Expiration);

                // Get the batch to delete.
                var expiredGrants = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .AsNoTracking()
                    .ToArrayAsync(ct);

                found = expiredGrants.Length;

                if (found > 0)
                {
                    _logger.LogInformation("Removing {grantCount} expired grants", found);

                    var foundIds = expiredGrants.Select(pg => pg.Id).ToArray();

                    // Using two where clauses should be more DB engine friendly as the
                    // first clause can be resolved using the expiration index.
                    var deleteCount = await query
                        // Run the same query, but now use an interval instead of Take(). This is to
                        // ensure we get all the elements, even if a new element was added in the middle
                        // of the set.
                        .Where(pg =>
                            pg.Expiration >= expiredGrants.First().Expiration
                            && pg.Expiration <= expiredGrants.Last().Expiration)
                        // To be on the safe side, filter out any possibly newly added item within the interval
                        .Where(pg => foundIds.Contains(pg.Id))
                        // And delete them.
                        .ExecuteDeleteAsync(ct);

                    if (deleteCount != found)
                    {
                        _logger.LogWarning("Tried to remove {grantCount} expired grants, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }

                    await _operationalStoreNotification.PersistedGrantsRemovedAsync(expiredGrants, ct);
                }
            }
        }
    }

    /// <summary>
    /// Removes the consumed persisted grants.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task RemoveConsumedPersistedGrantsAsync(Ct ct)
    {
        var found = int.MaxValue;

        var delay = TimeSpan.FromSeconds(_options.ConsumedTokenCleanupDelay);
        var consumedTimeThreshold = _timeProvider.GetUtcNow().UtcDateTime.Subtract(delay);

        if (_operationalStoreNotification == null)
        {
            var query = _persistedGrantDbContext.PersistedGrants
                .Where(x => x.ConsumedTime < consumedTimeThreshold)
                .OrderBy(pg => pg.ConsumedTime);

            while (found >= _options.TokenCleanupBatchSize)
            {
                found = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .ExecuteDeleteAsync(ct);

                if (found > 0)
                {
                    _logger.LogInformation("Removed {grantCount} consumed grants", found);
                }
            }
        }
        else
        {
            while (found >= _options.TokenCleanupBatchSize)
            {
                var query = _persistedGrantDbContext.PersistedGrants
                    .Where(x => x.ConsumedTime < consumedTimeThreshold)
                    .OrderBy(pg => pg.ConsumedTime);

                var consumedGrants = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .AsNoTracking()
                    .ToArrayAsync(ct);

                found = consumedGrants.Length;

                if (found > 0)
                {
                    _logger.LogInformation("Removing {grantCount} consumed grants", found);

                    var foundIds = consumedGrants.Select(pg => pg.Id).ToArray();

                    var deleteCount = await query
                        .Where(pg =>
                            pg.ConsumedTime >= consumedGrants.First().ConsumedTime
                            && pg.ConsumedTime <= consumedGrants.Last().ConsumedTime)
                        .Where(pg => foundIds.Contains(pg.Id))
                        .ExecuteDeleteAsync(ct);

                    if (deleteCount != found)
                    {
                        _logger.LogWarning("Tried to remove {grantCount} consumed grants, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }

                    await _operationalStoreNotification.PersistedGrantsRemovedAsync(consumedGrants, ct);
                }
            }
        }
    }


    /// <summary>
    /// Removes the stale device codes.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task RemoveDeviceCodesAsync(Ct ct)
    {
        var found = int.MaxValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (_operationalStoreNotification == null)
        {
            var query = _persistedGrantDbContext.DeviceFlowCodes
                .Where(x => x.Expiration < now)
                .OrderBy(x => x.Expiration);

            while (found >= _options.TokenCleanupBatchSize)
            {
                found = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .ExecuteDeleteAsync(ct);

                if (found > 0)
                {
                    _logger.LogInformation("Removed {deviceCodeCount} device flow codes", found);
                }
            }
        }
        else
        {
            while (found >= _options.TokenCleanupBatchSize)
            {
                var query = _persistedGrantDbContext.DeviceFlowCodes
                    .Where(x => x.Expiration < now)
                    .OrderBy(x => x.Expiration);

                var expiredCodes = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .AsNoTracking()
                    .ToArrayAsync(ct);

                found = expiredCodes.Length;

                if (found > 0)
                {
                    _logger.LogInformation("Removing {deviceCodeCount} device flow codes", found);

                    var foundCodes = expiredCodes.Select(c => c.DeviceCode).ToArray();

                    var deleteCount = await query
                        .Where(c => c.Expiration >= expiredCodes.First().Expiration && c.Expiration <= expiredCodes.Last().Expiration)
                        .Where(c => foundCodes.Contains(c.DeviceCode))
                        .ExecuteDeleteAsync(ct);

                    if (deleteCount != found)
                    {
                        _logger.LogWarning("Tried to remove {grantCount} expired device codes, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }

                    await _operationalStoreNotification.DeviceCodesRemovedAsync(expiredCodes, ct);
                }
            }
        }
    }

    /// <summary>
    /// Removes stale pushed authorization requests.
    /// </summary>
    protected virtual async Task RemovePushedAuthorizationRequestsAsync(Ct ct)
    {
        var found = int.MaxValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;


        while (found >= _options.TokenCleanupBatchSize)
        {
            var query = _persistedGrantDbContext.PushedAuthorizationRequests
                .Where(par => par.ExpiresAtUtc < now)
                .OrderBy(par => par.ExpiresAtUtc);

            var expiredPars = await query
                .Select(par => new { par.Id, par.ExpiresAtUtc })
                .Take(_options.TokenCleanupBatchSize)
                .AsNoTracking()
                .ToArrayAsync(ct);

            found = expiredPars.Length;

            if (found > 0)
            {
                _logger.LogInformation("Removing {parCount} stale pushed authorization requests", found);

                var foundIds = expiredPars.Select(par => par.Id).ToArray();

                var deleteCount = await query
                    .Where(par => par.ExpiresAtUtc >= expiredPars.First().ExpiresAtUtc && par.ExpiresAtUtc <= expiredPars.Last().ExpiresAtUtc)
                    .Where(par => foundIds.Contains(par.Id))
                    .ExecuteDeleteAsync(ct);

                if (deleteCount != found)
                {
                    _logger.LogWarning("Tried to remove {parCount} stale pushed authorization requests, but only {deleteCount} " +
                        "items were deleted. This indicates that another process has already removed the items.",
                        found, deleteCount);
                }
            }
        }
    }

    /// <summary>
    /// Removes stale SAML signin states.
    /// </summary>
    protected virtual async Task RemoveSamlSigninStatesAsync(Ct ct)
    {
        var found = int.MaxValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (_operationalStoreNotification == null)
        {
            var query = _persistedGrantDbContext.SamlSigninStates
                .Where(s => s.ExpiresAtUtc < now)
                .OrderBy(s => s.ExpiresAtUtc);

            while (found >= _options.TokenCleanupBatchSize)
            {
                found = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .ExecuteDeleteAsync(ct);

                if (found > 0)
                {
                    _logger.LogInformation("Removed {count} stale SAML signin states", found);
                }
            }
        }
        else
        {
            while (found >= _options.TokenCleanupBatchSize)
            {
                var query = _persistedGrantDbContext.SamlSigninStates
                    .Where(s => s.ExpiresAtUtc < now)
                    .OrderBy(s => s.ExpiresAtUtc);

                var expiredStates = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .AsNoTracking()
                    .ToArrayAsync(ct);

                found = expiredStates.Length;

                if (found > 0)
                {
                    _logger.LogInformation("Removing {count} stale SAML signin states", found);

                    var foundIds = expiredStates.Select(s => s.Id).ToArray();

                    var deleteCount = await query
                        .Where(s => s.ExpiresAtUtc >= expiredStates.First().ExpiresAtUtc && s.ExpiresAtUtc <= expiredStates.Last().ExpiresAtUtc)
                        .Where(s => foundIds.Contains(s.Id))
                        .ExecuteDeleteAsync(ct);

                    if (deleteCount != found)
                    {
                        _logger.LogWarning("Tried to remove {count} stale SAML signin states, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }

                    await _operationalStoreNotification.SamlSigninStatesRemovedAsync(expiredStates, ct);
                }
            }
        }
    }

    /// <summary>
    /// Removes expired SAML logout sessions.
    /// </summary>
    protected virtual async Task RemoveSamlLogoutSessionsAsync(Ct ct)
    {
        var found = int.MaxValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (_operationalStoreNotification == null)
        {
            var query = _persistedGrantDbContext.SamlLogoutSessions
                .Where(s => s.ExpiresAtUtc < now)
                .OrderBy(s => s.ExpiresAtUtc);

            while (found >= _options.TokenCleanupBatchSize)
            {
                found = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .ExecuteDeleteAsync(ct);

                if (found > 0)
                {
                    _logger.LogInformation("Removed {count} expired SAML logout sessions", found);
                }
            }
        }
        else
        {
            while (found >= _options.TokenCleanupBatchSize)
            {
                var query = _persistedGrantDbContext.SamlLogoutSessions
                    .Where(s => s.ExpiresAtUtc < now)
                    .OrderBy(s => s.ExpiresAtUtc);

                var expiredSessions = await query
                    .Take(_options.TokenCleanupBatchSize)
                    .AsNoTracking()
                    .ToArrayAsync(ct);

                found = expiredSessions.Length;

                if (found > 0)
                {
                    _logger.LogInformation("Removing {count} expired SAML logout sessions", found);

                    var foundIds = expiredSessions.Select(s => s.Id).ToArray();

                    var deleteCount = await query
                        .Where(s => s.ExpiresAtUtc >= expiredSessions.First().ExpiresAtUtc && s.ExpiresAtUtc <= expiredSessions.Last().ExpiresAtUtc)
                        .Where(s => foundIds.Contains(s.Id))
                        .ExecuteDeleteAsync(ct);

                    if (deleteCount != found)
                    {
                        _logger.LogWarning("Tried to remove {count} expired SAML logout sessions, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }

                    await _operationalStoreNotification.SamlLogoutSessionsRemovedAsync(expiredSessions, ct);
                }
            }
        }
    }
}
