// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// A paged result of outbox events ordered by sequence number.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="Events">The outbox events in this page.</param>
/// <param name="HasMore">Whether there are more events beyond this page.</param>
public sealed record OutboxEventsPage(IReadOnlyList<PersistedOutboxEvent> Events, bool HasMore);
