// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for the background job that periodically purges expired entities from
/// the storage layer (persisted grants, device codes, pushed authorization requests,
/// server-side sessions, SAML signin states, and SAML logout sessions).
/// </summary>
public class StoragePurgeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the background purge job is enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. Disable this if you want to manage expiration cleanup
    /// externally (for example, via a separate job scheduler).
    /// </remarks>
    public bool EnablePurge { get; set; } = true;

    /// <summary>
    /// Gets or sets how often the background job runs to purge expired entities.
    /// </summary>
    /// <remarks>
    /// Defaults to 1 hour. Only relevant when <see cref="EnablePurge"/> is <c>true</c>.
    /// Values less than 1 second are clamped to 1 second at runtime to prevent tight polling loops.
    /// </remarks>
    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum number of expired entities deleted in a single batch.
    /// </summary>
    /// <remarks>
    /// Defaults to 100. The purge job loops until fewer than <see cref="BatchSize"/> records
    /// are deleted, indicating all expired entities have been processed. Tune this value to
    /// balance cleanup throughput against database load. Values outside the range [1, 1000]
    /// are clamped at runtime to stay within the storage layer's supported bounds.
    /// </remarks>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether the initial start time of the purge job is
    /// randomized to reduce the likelihood of concurrent cleanup conflicts when multiple
    /// server instances are running.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. When enabled, the first purge run is scheduled at a random
    /// time between host startup and the first <see cref="PurgeInterval"/>. Subsequent runs
    /// follow the configured interval.
    /// </remarks>
    public bool FuzzStartup { get; set; } = true;
}
