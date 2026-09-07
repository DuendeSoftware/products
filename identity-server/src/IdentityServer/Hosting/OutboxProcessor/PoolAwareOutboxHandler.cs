// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.Internal.Outbox;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Decorator that establishes the ambient pool context from the event's <see cref="PersistedOutboxEvent.PoolId"/>
/// before delegating to the inner handler. This ensures downstream services (stores, repositories)
/// resolve the correct pool without the processor host needing to know about pool semantics.
/// </summary>
internal sealed class PoolAwareOutboxHandler(
    IOutboxSubscriberHandler inner,
    IPoolContextAccessor poolContextAccessor) : IOutboxSubscriberHandler
{
    public async Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
    {
        var previousPoolId = poolContextAccessor.CurrentPoolId;
        try
        {
            poolContextAccessor.CurrentPoolId = item.PoolId;
            return await inner.HandleAsync(item, ct);
        }
        finally
        {
            poolContextAccessor.CurrentPoolId = previousPoolId;
        }
    }
}
