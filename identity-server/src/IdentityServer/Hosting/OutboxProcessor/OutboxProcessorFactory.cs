// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Creates fresh <see cref="StorageOutboxProcessor"/> instances. <see cref="OutboxProcessorHost"/>
/// creates a new one whenever it newly acquires the outbox processor's lease, so the processor's
/// in-memory retry state never survives a handover to another node.
/// </summary>
internal sealed class OutboxProcessorFactory(IServiceProvider serviceProvider)
{
    /// <summary>Builds a new <see cref="StorageOutboxProcessor"/>, resolving its dependencies from DI.</summary>
    public StorageOutboxProcessor Create() => ActivatorUtilities.CreateInstance<StorageOutboxProcessor>(serviceProvider);
}
