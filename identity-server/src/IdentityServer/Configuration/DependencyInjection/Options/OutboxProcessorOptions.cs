// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for the background service that periodically processes outbox events and
/// dispatches them to registered handlers (e.g., back-channel logout for expired sessions).
/// </summary>
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
    /// Gets or sets the maximum number of outbox events fetched in a single batch.
    /// </summary>
    /// <remarks>
    /// Defaults to 100. Values outside the range [1, 1000] are clamped at runtime.
    /// </remarks>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for a failed event before it is
    /// force-dropped (deleted without further processing).
    /// </summary>
    /// <remarks>
    /// Defaults to 3. There is no dead letter queue; once max retries are exhausted the event
    /// is permanently discarded.
    /// </remarks>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retry attempts for a failed event.
    /// </summary>
    /// <remarks>
    /// Defaults to 1 minute. The actual delay increases exponentially based on
    /// <see cref="RetryBackoffMultiplier"/> up to <see cref="MaxRetryDelay"/>.
    /// </remarks>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the exponential backoff multiplier applied to <see cref="RetryDelay"/>
    /// on successive retry attempts.
    /// </summary>
    /// <remarks>
    /// Defaults to 2.0. For example, with a base delay of 1 minute and multiplier of 2.0,
    /// retry delays would be 1 min, 2 min, 4 min, etc.
    /// </remarks>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts, capping the exponential backoff.
    /// </summary>
    /// <remarks>
    /// Defaults to 30 minutes. Ensures that backoff does not grow unbounded for high retry counts.
    /// </remarks>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(30);

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
