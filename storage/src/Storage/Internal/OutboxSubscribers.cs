// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Outbox;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;

namespace Duende.Storage.Internal;

/// <summary>
/// Collects all enabled <see cref="IOutboxSubscriber"/> registrations from DI and provides
/// efficient lookup of matching subscribers for a given event and entity type.
/// Subscribers with <see cref="IOutboxSubscriber.IsEnabled"/> set to <c>false</c> are excluded.
/// </summary>
internal sealed class OutboxSubscribers
{
    private readonly IReadOnlyList<IOutboxSubscriber> _subscribers;

    /// <summary>
    /// Initializes the registry with the resolved set of subscribers, filtering out disabled ones.
    /// </summary>
    public OutboxSubscribers(IEnumerable<IOutboxSubscriber> subscribers) =>
        _subscribers = [.. subscribers.Where(s => s.IsEnabled)];

    /// <summary>Returns true when no enabled subscribers are registered.</summary>
    public bool IsEmpty => _subscribers.Count == 0;

    /// <summary>All enabled subscribers.</summary>
    public IReadOnlyList<IOutboxSubscriber> Subscribers => _subscribers;

    /// <summary>
    /// Returns all enabled subscribers that match the given event name and entity type ID.
    /// An empty <see cref="IOutboxSubscriber.EntityTypeIds"/> matches all entity types (wildcard).
    /// An empty <see cref="IOutboxSubscriber.EventNames"/> matches all event names (wildcard).
    /// </summary>
    public IEnumerable<IOutboxSubscriber> GetMatchingSubscribers(OutboxEventName eventName, int entityTypeId) =>
        _subscribers.Where(s =>
            (s.EntityTypeIds.Count == 0 || s.EntityTypeIds.Contains(entityTypeId)) &&
            (s.EventNames.Count == 0 || s.EventNames.Contains(eventName)));

    public bool HasSubscriber(OutboxEventName eventName, int entityTypeId) => GetMatchingSubscribers(eventName, entityTypeId).Any();
}
