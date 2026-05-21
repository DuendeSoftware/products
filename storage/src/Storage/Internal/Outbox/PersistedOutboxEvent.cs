// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Represents an outbox event as read from the database, including the
/// database-assigned sequence number and the store-stamped space identifier.
/// </summary>
public sealed record PersistedOutboxEvent
{
    /// <summary>The store-generated unique identifier for this persisted message (one per subscriber fanout row).</summary>
    public required OutboxEventId MessageId { get; init; }

    /// <summary>The caller-supplied event identifier, shared across all subscriber copies of the same logical event.</summary>
    public required OutboxEventId EventId { get; init; }

    /// <summary>When the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Database-assigned monotonic sequence number for ordering and paging events.</summary>
    public required long SequenceNumber { get; init; }

    /// <summary>The name of the domain event (e.g. "UserCreated").</summary>
    public required OutboxEventName EventName { get; init; }

    /// <summary>The ID of the entity that is the subject of this event.</summary>
    public required UuidV7 SubjectId { get; init; }

    /// <summary>The name of the entity type (e.g. "User").</summary>
    public required string EntityTypeName { get; init; }

    /// <summary>The numeric ID of the entity type.</summary>
    public required int EntityTypeId { get; init; }

    /// <summary>The pool this event belongs to, stamped by the store at write time.</summary>
    public required PoolId PoolId { get; init; }

    /// <summary>The serialized event payload (typically JSON).</summary>
    public required string Payload { get; init; }

    /// <summary>The name of the subscriber this event was addressed to at write time.</summary>
    public required SubscriberName SubscriberName { get; init; }
}
