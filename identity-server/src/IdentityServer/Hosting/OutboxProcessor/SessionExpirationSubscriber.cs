// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.Internal.Outbox;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Outbox subscriber that listens for <see cref="OutboxEventName.EntityExpired"/> events
/// targeting server-side sessions (entity type 2107). Enables the outbox processor host to
/// trigger back-channel logout notifications for expired sessions.
/// </summary>
internal sealed class SessionExpirationSubscriber(IServiceProvider serviceProvider) : IOutboxSubscriber
{
    internal static readonly SubscriberName Name = SubscriberName.Create("SessionExpiration");

    /// <inheritdoc />
    public SubscriberName SubscriberName => Name;

    /// <inheritdoc />
    public bool IsEnabled =>
        serviceProvider.GetService(typeof(IStorageBackedSessionsMarker)) is not null &&
        serviceProvider.GetService(typeof(IServerSideSessionsMarker)) is not null;

    /// <inheritdoc />
    public IReadOnlySet<OutboxEventName> EventNames { get; } =
        new HashSet<OutboxEventName> { OutboxEventName.EntityExpired };

    /// <inheritdoc />
    public IReadOnlySet<int> EntityTypeIds { get; } =
        new HashSet<int> { (int)ServerSideSessionDso.EntityType.Id };
}
