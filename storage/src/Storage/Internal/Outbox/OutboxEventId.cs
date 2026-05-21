// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

[ValueOf<Guid>]
public partial record OutboxEventId
{
    /// <summary>
    /// Creates a new OutboxEventId using a version 7 UUID.
    /// </summary>
    public static OutboxEventId New() => new(Guid.CreateVersion7());
}
