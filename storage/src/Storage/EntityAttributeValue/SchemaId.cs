// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Identifies an attribute schema. Schema IDs are readable strings (e.g., <c>"client"</c>,
///     <c>"api-resource"</c>, <c>"idp:oidc"</c>) used as discriminated storage keys.
///     Comparison is case-insensitive.
/// </summary>
[StringValue]
public partial record SchemaId
{
    internal const int MaxLength = 50;

    internal static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9_\-:]*$")]
    internal static partial Regex Regex();

    static string Normalize(string value) => value.Trim();
}
