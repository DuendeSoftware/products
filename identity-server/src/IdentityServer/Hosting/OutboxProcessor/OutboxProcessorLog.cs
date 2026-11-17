// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

internal static partial class OutboxProcessorLog
{
    [LoggerMessage(
        EventName = nameof(StartingProcessor),
        Message = "Starting outbox processor service")]
    internal static partial void StartingProcessor(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(StoppingProcessor),
        Message = "Stopping outbox processor service")]
    internal static partial void StoppingProcessor(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(CancellationRequested),
        Message = "Cancellation requested during delay. Exiting outbox processor.")]
    internal static partial void CancellationRequested(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(DelayException),
        Message = "Task.Delay exception in outbox processor. Exiting.")]
    internal static partial void DelayException(this ILogger logger, LogLevel logLevel, Exception exception);

    [LoggerMessage(
        EventName = nameof(SkippedNotLeaseHolder),
        Message = "Skipping outbox processor cycle: another node holds the lease")]
    internal static partial void SkippedNotLeaseHolder(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(LeaseLost),
        Message = "Outbox processor lease was lost while a cycle was running")]
    internal static partial void LeaseLost(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(LockUnavailable),
        Message = "Outbox processor could not reach the distributed lock; skipping this cycle")]
    internal static partial void LockUnavailable(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(NewLeaseFreshProcessor),
        Message = "Outbox processor lease newly acquired; creating a fresh processor")]
    internal static partial void NewLeaseFreshProcessor(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = "SessionHandlerDrop",
        Message = "Dropping outbox event {MessageId}: {Reason}")]
    internal static partial void SessionHandlerPayloadDrop(this ILogger logger, LogLevel logLevel, Guid messageId, string reason);

    [LoggerMessage(
        EventName = "SessionHandlerDropWithException",
        Message = "Dropping outbox event {MessageId}: {Reason}")]
    internal static partial void SessionHandlerPayloadDrop(this ILogger logger, LogLevel logLevel, Exception exception, Guid messageId, string reason);

    [LoggerMessage(
        EventName = "SessionHandlerException",
        Message = "Exception processing outbox event {MessageId}: {ExceptionMessage}")]
    internal static partial void SessionHandlerPayloadException(this ILogger logger, LogLevel logLevel, Exception exception, Guid messageId, string exceptionMessage);
}
