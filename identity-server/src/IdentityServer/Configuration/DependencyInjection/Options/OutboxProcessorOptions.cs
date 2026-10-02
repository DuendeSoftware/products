// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Hosting settings for the background service that drives outbox processing.
/// </summary>
/// <remarks>
/// Only hosting concerns live here: whether the processor runs, how often, and startup jitter.
/// How each cycle drains the outbox, meaning batch size, retry count and backoff, is configured
/// on Duende.Storage's own outbox options via <c>ConfigureOutboxProcessor()</c> when registering
/// storage.
/// </remarks>
public class OutboxProcessorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the background outbox processor service is enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. Disable this if you want to manage outbox consumption
    /// externally.
    /// </remarks>
    public bool EnableProcessor { get; set; } = true;

    /// <summary>
    /// Gets or sets how often the background service runs to process outbox events.
    /// </summary>
    /// <remarks>
    /// Defaults to 30 seconds. Values less than 1 second are clamped to 1 second at runtime
    /// to prevent tight polling loops.
    /// </remarks>
    public TimeSpan ProcessInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether the initial start time of the processor service is
    /// randomized to reduce the likelihood of concurrent processing conflicts when multiple
    /// server instances are running.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. When enabled, the first processor run is scheduled at a random
    /// time between host startup and the first <see cref="ProcessInterval"/>.
    /// </remarks>
    public bool FuzzStartup { get; set; } = true;
}