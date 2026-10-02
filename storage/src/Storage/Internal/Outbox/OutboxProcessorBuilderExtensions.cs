// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Extension methods for configuring outbox processing.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class OutboxProcessorBuilderExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Configures how the outbox processor drains subscription queues: batch size, retry count,
        /// and the retry backoff schedule.
        /// </summary>
        /// <remarks>
        /// Hosting concerns, meaning whether the processor runs at all and how often, belong to the
        /// host driving it rather than to these options. May be called more than once; each
        /// callback is applied in registration order.
        /// </remarks>
        public IStorageBuilder ConfigureOutboxProcessor(Action<OutboxProcessorOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            _ = builder.Services.Configure(configure);
            return builder;
        }
    }
}
