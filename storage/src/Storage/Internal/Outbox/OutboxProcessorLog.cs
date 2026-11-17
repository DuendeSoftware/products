// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Storage.Internal.Outbox;

internal static partial class OutboxProcessorLog
{
    [LoggerMessage(
        EventName = nameof(BatchProcessed),
        Message = "Outbox processor processed {Count} events for subscription {SubscriptionName}")]
    internal static partial void BatchProcessed(this ILogger logger, LogLevel logLevel, int count, string subscriptionName);

    [LoggerMessage(
        EventName = nameof(HandlerRetry),
        Message = "Outbox handler retry for subscription {SubscriptionName}, message {MessageId}: {Reason} (attempt {Attempt}/{MaxAttempts})")]
    internal static partial void HandlerRetry(this ILogger logger, LogLevel logLevel, string subscriptionName, Guid messageId, string reason, int attempt, int maxAttempts);

    [LoggerMessage(
        EventName = nameof(HandlerDrop),
        Message = "Outbox handler drop for subscription {SubscriptionName}, message {MessageId}: {Reason}")]
    internal static partial void HandlerDrop(this ILogger logger, LogLevel logLevel, string subscriptionName, Guid messageId, string reason);

    [LoggerMessage(
        EventName = nameof(ForceDrop),
        Message = "Dropping outbox message {MessageId} for subscription {SubscriptionName} after {Attempts} failed attempts")]
    internal static partial void ForceDrop(this ILogger logger, LogLevel logLevel, Guid messageId, string subscriptionName, int attempts);

    [LoggerMessage(
        EventName = nameof(HandlerException),
        Message = "Outbox handler exception for subscription {SubscriptionName}, message {MessageId} (attempt {Attempt}/{MaxAttempts}): {ExceptionMessage}")]
    internal static partial void HandlerException(this ILogger logger, LogLevel logLevel, Exception exception, string subscriptionName, Guid messageId, int attempt, int maxAttempts, string exceptionMessage);

    [LoggerMessage(
        EventName = nameof(HandlerNotFound),
        Message = "No IOutboxSubscriptionHandler registered for subscription {SubscriptionName}")]
    internal static partial void HandlerNotFound(this ILogger logger, LogLevel logLevel, string subscriptionName);

    [LoggerMessage(
        EventName = nameof(ProcessorException),
        Message = "Exception processing outbox events for subscription {SubscriptionName}")]
    internal static partial void ProcessorException(this ILogger logger, LogLevel logLevel, Exception exception, string subscriptionName);

    [LoggerMessage(
        EventName = nameof(ProcessorCancelled),
        Message = "Outbox processor cancelled")]
    internal static partial void ProcessorCancelled(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ContextEstablishmentFailed),
        Message = "Ambient context establishment failed for subscription {SubscriptionName}, pool {PoolId}, message {MessageId} (attempt {Attempt}/{MaxAttempts})")]
    internal static partial void ContextEstablishmentFailed(this ILogger logger, LogLevel logLevel, Exception exception, string subscriptionName, int poolId, Guid messageId, int attempt, int maxAttempts);
}
