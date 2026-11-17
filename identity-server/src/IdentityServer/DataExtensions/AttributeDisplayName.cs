// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

namespace Duende.IdentityServer.DataExtensions;

/// <summary>
///     Represents a human-readable display name for an attribute or attribute group.
/// </summary>
[StringValue]
public partial record AttributeDisplayName
{
    internal const int MaxLength = 200;

    static string Normalize(string value) => value.Trim();
}
