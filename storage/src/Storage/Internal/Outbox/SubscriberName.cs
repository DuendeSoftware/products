// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// The unique name identifying an outbox subscriber. Only alphanumeric characters, underscores, and hyphens are allowed.
/// </summary>
[StringValue]
public partial record SubscriberName
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex Regex();
}
