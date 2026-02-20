// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Extensions;

/// <summary>
/// Extension methods for DbContext
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Saves changes and handles concurrency exceptions.
    /// </summary>
    public static async Task<ICollection<T>> SaveChangesWithConcurrencyCheckAsync<T>(this IPersistedGrantDbContext context, ILogger logger, CT ct = default)
        where T : class
    {
        var list = new List<T>();

        var count = 3;

        while (count > 0)
        {
            try
            {
                await context.SaveChangesAsync(ct);
                return list;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                count--;

                // we get this if/when someone else already deleted the records
                // we want to essentially ignore this, and keep working
                logger.LogDebug("Concurrency exception removing records: {exception}", ex.Message);

                foreach (var entry in ex.Entries)
                {
                    // mark this entry as not attached anymore so we don't try to re-delete
                    entry.State = EntityState.Detached;
                    list.Add((T)entry.Entity);
                }
            }
        }

        logger.LogDebug("Too many concurrency exceptions. Exiting.");

        return list;
    }
}
