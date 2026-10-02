// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Outbox;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;

namespace Duende.Storage.Internal;

/// <summary>
/// Collects all enabled <see cref="IOutboxSubscription"/> registrations from DI and provides
/// efficient lookup of matching subscriptions for a given event and entity type.
/// Subscriptions with <see cref="IOutboxSubscription.IsEnabled"/> set to <c>false</c> are excluded.
/// </summary>
internal sealed class OutboxSubscriptions
{
    private readonly IReadOnlyList<IOutboxSubscription> _subscriptions;

    /// <summary>
    /// Initializes the registry with the resolved set of subscriptions, filtering out disabled ones.
    /// </summary>
    public OutboxSubscriptions(IEnumerable<IOutboxSubscription> subscriptions) =>
        _subscriptions = [.. subscriptions.Where(s => s.IsEnabled)];

    /// <summary>Returns true when no enabled subscriptions are registered.</summary>
    public bool IsEmpty => _subscriptions.Count == 0;

    /// <summary>All enabled subscriptions.</summary>
    public IReadOnlyList<IOutboxSubscription> Subscriptions => _subscriptions;

    /// <summary>
    /// Returns all enabled subscriptions that match the given event name and entity type ID.
    /// An empty <see cref="IOutboxSubscription.EntityTypeIds"/> matches all entity types (wildcard).
    /// An empty <see cref="IOutboxSubscription.EventNames"/> matches all event names (wildcard).
    /// </summary>
    public IEnumerable<IOutboxSubscription> GetMatchingSubscriptions(OutboxEventName eventName, int entityTypeId) =>
        _subscriptions.Where(s =>
            (s.EntityTypeIds.Count == 0 || s.EntityTypeIds.Contains(entityTypeId)) &&
            (s.EventNames.Count == 0 || s.EventNames.Contains(eventName)));

    public bool HasSubscription(OutboxEventName eventName, int entityTypeId) => GetMatchingSubscriptions(eventName, entityTypeId).Any();
}
