// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// A paged result of outbox events ordered by sequence number.
/// </summary>
/// <param name="Events">The outbox events in this page.</param>
/// <param name="HasMore">Whether there are more events beyond this page.</param>
public sealed record OutboxEventsPage(IReadOnlyList<PersistedOutboxEvent> Events, bool HasMore);
