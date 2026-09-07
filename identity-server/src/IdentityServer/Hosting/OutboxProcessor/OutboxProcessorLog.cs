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
        EventName = nameof(BatchProcessed),
        Message = "Outbox processor processed {Count} events for subscriber {SubscriberName}")]
    internal static partial void BatchProcessed(this ILogger logger, LogLevel logLevel, int count, string subscriberName);

    [LoggerMessage(
        EventName = nameof(HandlerRetry),
        Message = "Outbox handler retry for subscriber {SubscriberName}, message {MessageId}: {Reason} (attempt {Attempt}/{MaxRetries}, retry after {RetryAfterUtc})")]
    internal static partial void HandlerRetry(this ILogger logger, LogLevel logLevel, string subscriberName, Guid messageId, string reason, int attempt, int maxRetries, DateTimeOffset retryAfterUtc);

    [LoggerMessage(
        EventName = nameof(HandlerDrop),
        Message = "Outbox handler drop for subscriber {SubscriberName}, message {MessageId}: {Reason}")]
    internal static partial void HandlerDrop(this ILogger logger, LogLevel logLevel, string subscriberName, Guid messageId, string reason);

    [LoggerMessage(
        EventName = nameof(ForceDrop),
        Message = "Force-dropping outbox message {MessageId} for subscriber {SubscriberName} after {MaxRetries} retries exhausted")]
    internal static partial void ForceDrop(this ILogger logger, LogLevel logLevel, Guid messageId, string subscriberName, int maxRetries);

    [LoggerMessage(
        EventName = nameof(HandlerException),
        Message = "Outbox handler exception for subscriber {SubscriberName}, message {MessageId} (attempt {Attempt}/{MaxRetries}): {ExceptionMessage}")]
    internal static partial void HandlerException(this ILogger logger, LogLevel logLevel, Exception exception, string subscriberName, Guid messageId, int attempt, int maxRetries, string exceptionMessage);

    [LoggerMessage(
        EventName = nameof(HandlerNotFound),
        Message = "No IOutboxSubscriberHandler registered for subscriber {SubscriberName}")]
    internal static partial void HandlerNotFound(this ILogger logger, LogLevel logLevel, string subscriberName);

    [LoggerMessage(
        EventName = nameof(RetryNotYetEligible),
        Message = "Outbox processor skipping subscriber {SubscriberName}: retry not yet eligible until {RetryAfterUtc}")]
    internal static partial void RetryNotYetEligible(this ILogger logger, LogLevel logLevel, string subscriberName, DateTimeOffset retryAfterUtc);

    [LoggerMessage(
        EventName = nameof(CancellationRequested),
        Message = "Cancellation requested during delay. Exiting outbox processor.")]
    internal static partial void CancellationRequested(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ProcessorException),
        Message = "Exception processing outbox events for subscriber {SubscriberName}")]
    internal static partial void ProcessorException(this ILogger logger, LogLevel logLevel, Exception exception, string subscriberName);

    [LoggerMessage(
        EventName = nameof(ProcessorCancelled),
        Message = "Outbox processor cancelled")]
    internal static partial void ProcessorCancelled(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(DelayException),
        Message = "Task.Delay exception in outbox processor. Exiting.")]
    internal static partial void DelayException(this ILogger logger, LogLevel logLevel, Exception exception);

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
