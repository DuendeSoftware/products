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

    /// <summary>
    /// Constructor for TokenCleanupService.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="persistedGrantDbContext"></param>
    /// <param name="operationalStoreNotification"></param>
    /// <param name="logger"></param>
    public TokenCleanupService(
        OperationalStoreOptions options,
        IPersistedGrantDbContext persistedGrantDbContext,
        ILogger<TokenCleanupService> logger,
        IOperationalStoreNotification operationalStoreNotification = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.TokenCleanupBatchSize < 1)
        {
            throw new ArgumentException("Token cleanup batch size interval must be at least 1");
        }

        _persistedGrantDbContext = persistedGrantDbContext ?? throw new ArgumentNullException(nameof(persistedGrantDbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _operationalStoreNotification = operationalStoreNotification;
    }

    /// <inheritdoc/>
    public async Task CleanupGrantsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogTrace("Querying for expired grants to remove");

            await RemoveGrantsAsync(cancellationToken);
            await RemoveDeviceCodesAsync(cancellationToken);
            await RemovePushedAuthorizationRequestsAsync(cancellationToken);
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
    protected virtual async Task RemoveGrantsAsync(CancellationToken cancellationToken = default)
    {
        await RemoveExpiredPersistedGrantsAsync(cancellationToken);
        if (_options.RemoveConsumedTokens)
        {
            await RemoveConsumedPersistedGrantsAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Removes the expired persisted grants.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task RemoveExpiredPersistedGrantsAsync(CancellationToken cancellationToken = default)
    {
        var found = int.MaxValue;

        while (found >= _options.TokenCleanupBatchSize)
        {
            // Filter and order on expiration which is indexed, this allows the
            // DB engine to just take the first N items from the index
            var query = _persistedGrantDbContext.PersistedGrants
                .Where(x => x.Expiration < DateTime.UtcNow)
                .OrderBy(x => x.Expiration);

            // Get the batch to delete.
            var expiredGrants = await query
                .Take(_options.TokenCleanupBatchSize)
                .AsNoTracking()
                .ToArrayAsync(cancellationToken);

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
                    .ExecuteDeleteAsync(cancellationToken);

                if (deleteCount != found)
                {
                    if (_operationalStoreNotification != null)
                    {
                        _logger.LogWarning("Tried to remove {grantCount} expired grants, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }
                    else
                    {
                        _logger.LogDebug("Tried to remove {grantCount} expired grants, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items.",
                            found, deleteCount);
                    }
                }

                if (_operationalStoreNotification != null)
                {
                    await _operationalStoreNotification.PersistedGrantsRemovedAsync(expiredGrants, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Removes the consumed persisted grants.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task RemoveConsumedPersistedGrantsAsync(CancellationToken cancellationToken = default)
    {
        var found = int.MaxValue;

        var delay = TimeSpan.FromSeconds(_options.ConsumedTokenCleanupDelay);
        var consumedTimeThreshold = DateTime.UtcNow.Subtract(delay);

        while (found >= _options.TokenCleanupBatchSize)
        {
            var query = _persistedGrantDbContext.PersistedGrants
                .Where(x => x.ConsumedTime < consumedTimeThreshold)
                .OrderBy(pg => pg.ConsumedTime);

            var consumedGrants = await query
                .Take(_options.TokenCleanupBatchSize)
                .AsNoTracking()
                .ToArrayAsync(cancellationToken);

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
                    .ExecuteDeleteAsync(cancellationToken);

                if (deleteCount != found)
                {
                    if (_operationalStoreNotification != null)
                    {
                        _logger.LogWarning("Tried to remove {grantCount} consumed grants, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }
                    else
                    {
                        _logger.LogDebug("Tried to remove {grantCount} consumed grants, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items.",
                            found, deleteCount);
                    }
                }

                if (_operationalStoreNotification != null)
                {
                    await _operationalStoreNotification.PersistedGrantsRemovedAsync(consumedGrants, cancellationToken);
                }
            }
        }
    }


    /// <summary>
    /// Removes the stale device codes.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task RemoveDeviceCodesAsync(CancellationToken cancellationToken = default)
    {
        var found = int.MaxValue;

        while (found >= _options.TokenCleanupBatchSize)
        {
            var query = _persistedGrantDbContext.DeviceFlowCodes
                .Where(x => x.Expiration < DateTime.UtcNow)
                .OrderBy(x => x.Expiration);

            var expiredCodes = await query
                .Take(_options.TokenCleanupBatchSize)
                .AsNoTracking()
                .ToArrayAsync(cancellationToken);

            found = expiredCodes.Length;

            if (found > 0)
            {
                _logger.LogInformation("Removing {deviceCodeCount} device flow codes", found);

                var foundCodes = expiredCodes.Select(c => c.DeviceCode).ToArray();

                var deleteCount = await query
                    .Where(c => c.Expiration >= expiredCodes.First().Expiration && c.Expiration <= expiredCodes.Last().Expiration)
                    .Where(c => foundCodes.Contains(c.DeviceCode))
                    .ExecuteDeleteAsync(cancellationToken);

                if (deleteCount != found)
                {
                    if (_operationalStoreNotification != null)
                    {
                        _logger.LogWarning("Tried to remove {grantCount} expired device codes, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items. Duplicate " +
                            "notifications may be sent to the registered IOperationalStoreNotification.",
                            found, deleteCount);
                    }
                    else
                    {
                        _logger.LogDebug("Tried to remove {grantCount} expired device codes, but only {deleteCount} " +
                            "was deleted. This indicates that another process has already removed the items.",
                            found, deleteCount);
                    }
                }

                if (_operationalStoreNotification != null)
                {
                    await _operationalStoreNotification.DeviceCodesRemovedAsync(expiredCodes, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Removes stale pushed authorization requests.
    /// </summary>
    protected virtual async Task RemovePushedAuthorizationRequestsAsync(CancellationToken cancellationToken = default)
    {
        var found = int.MaxValue;

        var query = _persistedGrantDbContext.PushedAuthorizationRequests
            .Where(par => par.ExpiresAtUtc < DateTime.UtcNow)
            .OrderBy(par => par.ExpiresAtUtc);

        while (found >= _options.TokenCleanupBatchSize)
        {
            found = await query
                .Take(_options.TokenCleanupBatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            if (found > 0)
            {
                _logger.LogInformation("Removed {parCount} stale pushed authorization requests", found);
            }
        }
    }
}
