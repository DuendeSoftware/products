// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace Duende.Bff.EntityFramework;

/// <summary>
/// Abstraction for the session context.
/// </summary>
public interface ISessionDbContext
{
    /// <summary>
    /// Gets or sets the users sessions.
    /// </summary>
    /// <value>
    /// The users sessions.
    /// </value>

    public DbSet<UserSessionEntity> UserSessions { get; set; }

    /// <summary>
    /// Saves the changes.
    /// </summary>
    /// <returns></returns>
    Task<int> SaveChangesAsync(Ct ct = default);
}
