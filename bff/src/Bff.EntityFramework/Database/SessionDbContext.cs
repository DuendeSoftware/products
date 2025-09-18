// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;

namespace Duende.Bff.EntityFramework;

/// <summary>
/// DbContext for session entities
/// </summary>
public class SessionDbContext : SessionDbContext<SessionDbContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionDbContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    public SessionDbContext(DbContextOptions<SessionDbContext> options) : base(options)
    {
    }
}

/// <summary>
/// DbContext for session entities
/// </summary>
public class SessionDbContext<TContext> : DbContext, ISessionDbContext
    where TContext : DbContext, ISessionDbContext
{
    /// <summary>
    /// The options for the session store table schema.
    /// </summary>
    public SessionStoreOptions StoreOptions { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionDbContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    public SessionDbContext(DbContextOptions<TContext> options) : base(options)
    {
    }

    /// <summary>
    /// DbSet for user sessions
    /// </summary>
    public DbSet<UserSessionEntity> UserSessions { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (StoreOptions is null)
        {
            StoreOptions = this.GetService<IOptions<SessionStoreOptions>>()?.Value ?? new SessionStoreOptions();
        }

        ConfigureSchema(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Allows controlling the schema.
    /// </summary>
    protected virtual void ConfigureSchema(ModelBuilder modelBuilder) => modelBuilder.ConfigureSessionContext(StoreOptions);
}
