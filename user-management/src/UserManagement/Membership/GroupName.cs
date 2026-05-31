// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>Represents the display name of a group.</summary>
[StringValue]
public partial record GroupName
{
    internal const int MaxLength = 200;

    /// <summary>Gets the normalized string value of the group name.</summary>
    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static GroupName Load(string value) => new(value);
}
