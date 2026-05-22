// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Declares a subscriber that wants to receive outbox events via the fanout mechanism.
/// Subscribers are registered in DI and matched against outbox events by event name and entity type.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface IOutboxSubscriber
{
    /// <summary>The unique name identifying this subscriber.</summary>
    SubscriberName SubscriberName { get; }

    /// <summary>
    /// Whether this subscriber is currently enabled for outbox event delivery.
    /// Disabled subscribers are excluded from fanout and will not receive any events.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// The event names this subscriber listens to.
    /// An empty set means the subscriber receives all events (wildcard).
    /// A non-empty set means only the specified event names are received.
    /// </summary>
    IReadOnlySet<OutboxEventName> EventNames { get; }

    /// <summary>
    /// The entity type IDs this subscriber listens to.
    /// An empty set means the subscriber receives events for all entity types (wildcard).
    /// A non-empty set means only the specified entity type IDs are received.
    /// </summary>
    IReadOnlySet<int> EntityTypeIds { get; }
}
