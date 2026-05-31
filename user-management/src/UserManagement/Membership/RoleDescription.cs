// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>Represents a human-readable description of a role.</summary>
[StringValue]
public partial record RoleDescription
{
    internal const int MaxLength = 500;

    /// <summary>Gets the normalized string value of the role description.</summary>
    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static RoleDescription Load(string value) => new(value);
}
