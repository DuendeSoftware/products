// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// The name of a domain event written to the outbox. Only alphanumeric characters, underscores, and hyphens are allowed.
/// </summary>
[StringValue]
public partial record OutboxEventName
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex Regex();

    /// <summary>
    /// The well-known event name written when an entity is purged due to expiration.
    /// </summary>
    public static readonly OutboxEventName EntityExpired = Create("EntityExpired");
}
