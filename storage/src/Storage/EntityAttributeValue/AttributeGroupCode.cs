// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents an attribute group code. Preserves the original casing of the name
///     but compares case-insensitively (using ordinal ignore-case) for equality and hashing.
/// </summary>
[StringValue]
public partial record AttributeGroupCode
{
    internal const int MaxLength = 100;

    internal static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    [GeneratedRegex(@"^[a-zA-Z0-9_-]+$")]
    internal static partial Regex Regex();

    static string Normalize(string value) => value.Trim();
}
