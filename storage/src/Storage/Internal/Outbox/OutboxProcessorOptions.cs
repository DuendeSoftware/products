// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Processing knobs for <see cref="OutboxProcessor"/>, namely batch size and retry count. Hosting
/// concerns (whether the processor runs at all, how often, and startup jitter) are not part of
/// this type; they belong to whichever host drives the processor.
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
    /// Defaults to 100. Values outside the range [1, 1000] are clamped at runtime. Failed rows
    /// stay queued and are fetched again on later passes, so they still count against this
    /// batch size alongside new rows.
    /// </remarks>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets how many times a failed event is retried after its first attempt before it
    /// is dropped.
    /// </summary>
    /// <remarks>
    /// Defaults to 3, meaning a failing event is attempted up to 4 times in total. 0 means no
    /// retries: an event is dropped after its first failed attempt. Negative values are rejected
    /// at startup. A failure is a handler <c>Retry</c> outcome, an exception thrown by the handler,
    /// or an exception thrown while establishing the ambient processing context; cancellation
    /// does not count as a failure. There is no delay between attempts: a failed event is simply
    /// retried the next time the processor runs. The event is deleted in the same pass as its
    /// last failed attempt. Attempt counts are kept in memory only and reset when the process
    /// restarts. There is no dead letter queue; once an event's attempts are exhausted it is
    /// permanently discarded.
    /// </remarks>
    public int MaxRetries { get; set; } = 3;
}
