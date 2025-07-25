// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.EntityFrameworkCore;

namespace Duende.IdentityServer.EntityFramework.Options;

/// <summary>
/// Options for configuring the operational context.
/// </summary>
public class OperationalStoreOptions
{
    /// <summary>
    /// Callback to configure the EF DbContext.
    /// </summary>
    /// <value>
    /// The configure database context.
    /// </value>
    public Action<DbContextOptionsBuilder>? ConfigureDbContext { get; set; }

    /// <summary>
    /// Callback in DI resolve the EF DbContextOptions. If set, ConfigureDbContext will not be used.
    /// </summary>
    /// <value>
    /// The configure database context.
    /// </value>
    public Action<IServiceProvider, DbContextOptionsBuilder>? ResolveDbContextOptions { get; set; }

    /// <summary>
    /// Gets or sets the default schema.
    /// </summary>
    /// <value>
    /// The default schema.
    /// </value>
    public string? DefaultSchema { get; set; }

    /// <summary>
    /// Gets or sets the persisted grants table configuration.
    /// </summary>
    /// <value>
    /// The persisted grants.
    /// </value>
    public TableConfiguration PersistedGrants { get; set; } = new TableConfiguration("PersistedGrants");

    /// <summary>
    /// Gets or sets the device flow codes table configuration.
    /// </summary>
    /// <value>
    /// The device flow codes.
    /// </value>
    public TableConfiguration DeviceFlowCodes { get; set; } = new TableConfiguration("DeviceCodes");

    /// <summary>
    /// Gets or sets the keys table configuration.
    /// </summary>
    /// <value>
    /// The keys.
    /// </value>
    public TableConfiguration Keys { get; set; } = new TableConfiguration("Keys");

    /// <summary>
    /// Gets or sets the user sessions table configuration.
    /// </summary>
    /// <value>
    /// The keys.
    /// </value>
    public TableConfiguration ServerSideSessions { get; set; } = new TableConfiguration("ServerSideSessions");

    /// <summary>
    /// Gets or sets the pushed authorization requests configuration.
    /// </summary>
    public TableConfiguration PushedAuthorizationRequests { get; set; } = new TableConfiguration("PushedAuthorizationRequests");

    /// <summary>
    /// Gets or sets a value indicating whether stale entries will be automatically cleaned up from the database.
    /// This is implemented by periodically connecting to the database (according to the TokenCleanupInterval) from the hosting application.
    /// Defaults to false.
    /// </summary>
    /// <value>
    ///   <c>true</c> if [enable token cleanup]; otherwise, <c>false</c>.
    /// </value>
    public bool EnableTokenCleanup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether consumed tokens will be included in the automatic clean up.
    /// </summary>
    /// <value>
    ///   <c>true</c> if consumed tokens are to be included in cleanup; otherwise, <c>false</c>.
    /// </value>
    public bool RemoveConsumedTokens { get; set; }

    /// <summary>
    /// Gets or sets the consumed token cleanup delay (in seconds). The default
    /// is 0. This delay is the amount of time that must elapse before tokens
    /// marked as consumed can be deleted. Note that only refresh tokens with
    /// OneTimeOnly usage can be marked as consumed.
    /// </summary>
    public int ConsumedTokenCleanupDelay { get; set; }

    /// <summary>
    /// Gets or sets the token cleanup interval (in seconds). The default is 3600 (1 hour).
    /// </summary>
    /// <value>
    /// The token cleanup interval.
    /// </value>
    public int TokenCleanupInterval { get; set; } = 3600;

    /// <summary>
    /// If multiple nodes are running the token cleanup at the same time, there will be
    /// concurrency issues in the database updates. To reduce the risk, the startup time
    /// of the first run can be fuzzed (randomized). The default is <c>true</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> if startup time should be fuzzed, otherwise false.
    /// </value>
    public bool FuzzTokenCleanupStart { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of records to remove at a time. Defaults to 100.
    /// </summary>
    /// <value>
    /// The size of the token cleanup batch.
    /// </value>
    public int TokenCleanupBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or set if EF DbContext pooling is enabled.
    /// </summary>
    public bool EnablePooling { get; set; }

    /// <summary>
    /// Gets or set the pool size to use when DbContext pooling is enabled. If not set, the EF default is used.
    /// </summary>
    public int? PoolSize { get; set; }
}
