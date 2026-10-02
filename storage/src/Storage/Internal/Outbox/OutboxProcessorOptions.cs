// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Processing knobs for <see cref="OutboxProcessor"/>, plus the storage key it reads and
/// writes through. Hosting concerns (whether the processor runs at all, how often, and
/// startup jitter) are not part of this type; they belong to whichever host drives the
/// processor.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class OutboxProcessorOptions
{
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

}
