// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

namespace Duende.IdentityServer.DataExtensions;

/// <summary>
///     Represents a schema attribute description.
/// </summary>
[StringValue]
public partial record AttributeDescription
{
    internal const int MaxLength = 200;

    static string Normalize(string value) => value.Trim();
}
