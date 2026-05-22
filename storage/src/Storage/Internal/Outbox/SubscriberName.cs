// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// The unique name identifying an outbox subscriber. Only alphanumeric characters, underscores, and hyphens are allowed.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
[StringValue]
public partial record SubscriberName
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex Regex();
}
