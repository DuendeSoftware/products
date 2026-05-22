// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Represents the outcome of a handler processing an outbox event.
/// Handlers return one of: <see cref="Success"/>, <see cref="Retry"/>, or <see cref="Drop"/>.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public abstract record HandleOutcomeResult
{
    private HandleOutcomeResult() { }

    /// <summary>Returns a success outcome — the event was processed and should be deleted.</summary>
    public static HandleOutcomeResult Success() => SuccessResult.Instance;

    /// <summary>
    /// Returns a retry outcome — the event could not be processed and should be retried after a delay.
    /// </summary>
    /// <param name="reason">Human-readable reason for the retry request.</param>
    public static HandleOutcomeResult Retry(string reason) => new RetryResult(reason);

    /// <summary>
    /// Returns a drop outcome — the event cannot be processed and should be deleted without retry.
    /// </summary>
    /// <param name="reason">Human-readable reason for dropping the event.</param>
    public static HandleOutcomeResult Drop(string reason) => new DropResult(reason);

    /// <summary>Successful processing outcome.</summary>
    public sealed record SuccessResult : HandleOutcomeResult
    {
        internal static readonly SuccessResult Instance = new();

        private SuccessResult() { }
    }

    /// <summary>Retry-requested outcome.</summary>
    /// <param name="Reason">Human-readable reason for the retry request.</param>
    public sealed record RetryResult(string Reason) : HandleOutcomeResult;

    /// <summary>Drop-requested outcome.</summary>
    /// <param name="Reason">Human-readable reason for dropping the event.</param>
    public sealed record DropResult(string Reason) : HandleOutcomeResult;
}
