// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Pagination;

/// <summary>
/// Thrown when a <see cref="ContinuationToken"/> passed to a cursor-based query cannot be used:
/// it is malformed, uses an unsupported format version, or was issued for a different sort.
/// </summary>
/// <remarks>
/// Derives from <see cref="ArgumentException"/> so existing <c>catch (ArgumentException)</c> handlers
/// still work. <see cref="ArgumentException.ParamName"/> is always <c>"dataRange"</c>. Thrown before any
/// connection is opened.
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
#pragma warning disable CA1032 // The reason is a required part of this exception's contract; the standard parameterless/message-only constructors would allow constructing it without one.
public sealed class InvalidContinuationTokenException : ArgumentException
{
    /// <summary>
    /// The reason the token was rejected.
    /// </summary>
    public InvalidContinuationTokenReason Reason { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="InvalidContinuationTokenException"/>.
    /// </summary>
    /// <param name="reason">The reason the token was rejected.</param>
    /// <param name="message">A message describing the problem.</param>
    /// <param name="inner">An optional inner exception.</param>
    public InvalidContinuationTokenException(InvalidContinuationTokenReason reason, string message, Exception? inner = null)
        : base(message, "dataRange", inner) => Reason = reason;
}
#pragma warning restore CA1032
