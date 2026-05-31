// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>Represents the display name of a role.</summary>
[StringValue]
public partial record RoleName
{
    internal const int MaxLength = 200;

    /// <summary>Gets the normalized string value of the role name.</summary>
    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static RoleName Load(string value) => new(value);
}
