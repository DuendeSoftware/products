// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Querying;

/// <summary>
/// A SCIM-like search expression string (e.g., <c>displayName eq "Engineers"</c>).
/// Used to filter query results using SCIM filter syntax (RFC 7644 §3.4.2.2).
/// </summary>
[StringValue]
public partial record SearchExpression
{
    /// <summary>The maximum allowed length of a search expression.</summary>
    public const int MaxLength = 4000;
}
