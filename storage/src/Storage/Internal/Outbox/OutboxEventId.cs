// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Represents a unique identifier for an outbox event.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
[ValueOf<Guid>]
public partial record OutboxEventId
{
    /// <summary>
    /// Creates a new OutboxEventId using a version 7 UUID.
    /// </summary>
    public static OutboxEventId New() => new(Guid.CreateVersion7());
}
