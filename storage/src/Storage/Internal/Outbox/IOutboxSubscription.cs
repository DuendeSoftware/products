// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Declares a subscription that wants to receive outbox events via the fanout mechanism.
/// Subscriptions are registered in DI and matched against outbox events by event name and entity type.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface IOutboxSubscription
{
    /// <summary>The unique name identifying this subscription.</summary>
    SubscriberName SubscriberName { get; }

    /// <summary>
    /// Whether this subscription is currently enabled for outbox event delivery.
    /// Disabled subscriptions are excluded from fanout and will not receive any events.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// The event names this subscription listens to.
    /// An empty set means the subscription receives all events (wildcard).
    /// A non-empty set means only the specified event names are received.
    /// </summary>
    IReadOnlySet<OutboxEventName> EventNames { get; }

    /// <summary>
    /// The entity type IDs this subscription listens to.
    /// An empty set means the subscription receives events for all entity types (wildcard).
    /// A non-empty set means only the specified entity type IDs are received.
    /// </summary>
    IReadOnlySet<int> EntityTypeIds { get; }
}
