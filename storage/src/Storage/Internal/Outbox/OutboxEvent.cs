// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Represents an event to be written to the outbox table atomically alongside a domain operation.
/// The store implementation stamps <c>PoolId</c> from the ambient pool context, and the
/// database automatically assigns the <c>SequenceNumber</c> on insert.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record OutboxEvent
{
    /// <summary>The unique identifier for this outbox event.</summary>
    public required OutboxEventId Id { get; init; }

    /// <summary>When the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The name of the domain event (e.g. "UserCreated").</summary>
    public required OutboxEventName EventName { get; init; }

    /// <summary>The ID of the entity that is the subject of this event.</summary>
    public required UuidV7 SubjectId { get; init; }

    /// <summary>The name of the entity type (e.g. "User").</summary>
    public required string EntityTypeName { get; init; }

    /// <summary>The numeric ID of the entity type.</summary>
    public required int EntityTypeId { get; init; }

    /// <summary>The serialized event payload. Must be valid JSON.</summary>
    public required string Payload { get; init; }

    /// <summary>The schema version of the DSO type serialized in <see cref="Payload"/>. Null for domain events whose payload is not a DSO.</summary>
    public int? DsoTypeSchemaVersion { get; init; }
}
